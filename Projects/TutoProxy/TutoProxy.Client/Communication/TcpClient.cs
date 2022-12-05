using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {

    public class TcpClient : BaseClient {
        int? localPort = null;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly Timer forceCloseTimer;
        readonly Socket socket;

        bool shutdownReceive;
        bool shutdownTransmit;

        Int64 totalTransmitted;
        Int64 limitTotalTransmitted;
        Int64 totalReceived;

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            forceCloseTimer = new Timer(OnForceCloseTimedEvent);
            limitTotalTransmitted = -1;

            socket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, created");
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
            Debug.WriteLine($"tcp({localPort}), o-port: {OriginPort}, attempt to close");
            TryShutdown(SocketShutdown.Both);
        }

        void StartClosingTimer() {
            forceCloseTimer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public override async void Dispose() {
            base.Dispose();
            try {
                socket.Shutdown(SocketShutdown.Both);
            } catch(SocketException) { }
            try {
                await socket.DisconnectAsync(true);
            } catch(SocketException) { }
            forceCloseTimer.Change(Timeout.Infinite, Timeout.Infinite);
            socket.Close(100);
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
            GC.SuppressFinalize(this);
        }

        public async Task<bool> Connect(CancellationToken cancellationToken) {
            if(socket.Connected) {
                logger.Error($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, already connected");
                return false;
            }

            try {
                await socket.ConnectAsync(serverEndPoint);
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
                return false;
            }
            localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            _ = Task.Run(() => ReceivingStream(cancellationToken), cancellationToken);
            return true;
        }

        async Task ReceivingStream(CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);

            while(socket.Connected && !cts.IsCancellationRequested) {
                int receivedBytes;
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);
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

                var response = new TcpDataResponseModel() { Port = Port, OriginPort = OriginPort, Data = data };
                await dataTunnelClient.SendTcpResponse(response, cts.Token);
                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }
            if(!cancellationTokenSource.IsCancellationRequested) {
                await dataTunnelClient.DisconnectTcp(new SocketAddressModel() { Port = Port, OriginPort = OriginPort }, totalReceived, cancellationToken);

                TryShutdown(SocketShutdown.Receive);
            }
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            try {
                var transmitted = Interlocked.Add(ref totalTransmitted, await socket.SendAsync(payload, SocketFlags.None, cts.Token));

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
