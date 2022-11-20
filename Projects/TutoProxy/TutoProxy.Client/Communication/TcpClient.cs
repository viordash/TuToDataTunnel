using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
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
                clientsService.RemoveTcpClient(Port, OriginPort);
            } else {
                StartClosingTimer();
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

        public override async void Dispose() {
            try {
                socket.Shutdown(SocketShutdown.Both);
            } catch(SocketException) { }
            try {
                await socket.DisconnectAsync(true);
            } catch(SocketException) { }
            forceCloseTimer.Dispose();
            socket.Close(100);
            base.Dispose();
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
        }

        public bool Connect(CancellationToken cancellationToken) {
            if(socket.Connected) {
                logger.Error($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, already connected");
                return false;
            }

            try {
                socket.Connect(serverEndPoint);
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
                return false;
            }
            localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            _ = ReceivingStream(cancellationToken);
            return true;
        }

        async Task ReceivingStream(CancellationToken cancellationToken) {
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


                var response = new TcpDataResponseModel(Port, OriginPort, data);
                await dataTunnelClient.SendTcpResponse(response, cancellationToken);

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }

            await dataTunnelClient.DisconnectTcp(new SocketAddressModel(Port, OriginPort), totalReceived, cancellationToken);

            TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            try {
                var transmitted = Interlocked.Add(ref totalTransmitted, await socket.SendAsync(payload, SocketFlags.None, cancellationToken));

                var limit = Interlocked.Read(ref limitTotalTransmitted);
                if(limit >= 0) {
                    if(transmitted >= limit) {
                        TryShutdown(SocketShutdown.Send);
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
                logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{payload.ToShortDescriptions()}");
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
