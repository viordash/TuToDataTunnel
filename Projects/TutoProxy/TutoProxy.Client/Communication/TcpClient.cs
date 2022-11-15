using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {

    public class TcpClient : BaseClient<Socket> {
        int? localPort = null;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly Timer forceCloseTimer;

        bool shutdownReceive;
        bool shutdownTransmit;

        Int64 totalTransmitted;
        Int64 limitTotalTransmitted;
        Int64 totalReceived;

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            forceCloseTimer = new Timer(OnForceCloseTimedEvent);
            limitTotalTransmitted = -1;
        }

        void TryShutdown(SocketShutdown how) {
            lock(this) {
                switch(how) {
                    case SocketShutdown.Receive:
                        shutdownReceive = true;
                        if(socket.Connected) {
                            try {
                                socket.Shutdown(SocketShutdown.Receive);
                            } catch(Exception) { }
                        }
                        break;
                    case SocketShutdown.Send:
                        shutdownTransmit = true;
                        if(socket.Connected) {
                            try {
                                socket.Shutdown(SocketShutdown.Send);
                            } catch(Exception) { }
                        }
                        break;
                    case SocketShutdown.Both:
                        shutdownReceive = true;
                        shutdownTransmit = true;
                        break;
                }


                if(shutdownReceive && shutdownTransmit) {
                    if(socket.Connected) {
                        try {
                            socket.Shutdown(SocketShutdown.Both);
                        } catch(SocketException) { }
                        try {
                            socket.Disconnect(true);
                        } catch(SocketException) { }
                    }
                    clientsService.RemoveTcpClient(Port, OriginPort);
                    forceCloseTimer.Dispose();
                } else {
                    StartClosingTimer();
                }
            }
        }
        void OnForceCloseTimedEvent(object? state) {
            Debug.WriteLine($"tcp({localPort}) , o-port: {OriginPort}, attempt to close");
            TryShutdown(SocketShutdown.Both);
        }

        void StartClosingTimer() {
            forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        protected override void OnTimedEvent(object? state) { }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public override void Dispose() {
            forceCloseTimer.Dispose();
            socket.Close(100);
            base.Dispose();
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
        }

        async void ReceivingStream(CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                int receivedBytes;
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                } catch(SocketException ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                }
                totalReceived += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();

                dataTunnelClient.PushOutgoingTcpData(new TcpStreamDataModel(Port, OriginPort, 0, data), cancellationToken);

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }

            await dataTunnelClient.DisconnectTcp(new SocketAddressModel(Port, OriginPort), totalReceived, cancellationToken);

            TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendData(TcpStreamDataModel streamData, CancellationToken cancellationToken) {
            if(!socket.Connected) {
                try {
                    await socket.ConnectAsync(serverEndPoint, cancellationToken);
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                }
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
                _ = Task.Run(() => ReceivingStream(cancellationToken));
            }

            try {
                bool connected = socket.Connected;
                var transmitted = Interlocked.Add(ref totalTransmitted, await socket.SendAsync(streamData.Data, SocketFlags.None, cancellationToken));
                if(!connected) {
                    TryShutdown(SocketShutdown.Send);
                } else {
                    var limit = Interlocked.Read(ref limitTotalTransmitted);
                    if(limit >= 0) {
                        if(transmitted >= limit) {
                            TryShutdown(SocketShutdown.Send);
                        }
                    }
                }

            } catch(SocketException) {
                TryShutdown(SocketShutdown.Send);
            } catch(ObjectDisposedException) {
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }

            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{streamData.Data.ToShortDescriptions()}");
            }
        }


        public void Disconnect(Int64 transferLimit, CancellationToken cancellationToken) {
            if(Interlocked.Read(ref totalTransmitted) >= transferLimit) {
                TryShutdown(SocketShutdown.Both);
            } else {
                Interlocked.Exchange(ref limitTotalTransmitted, transferLimit);
                StartClosingTimer();
            }
        }

    }
}
