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

        public int TotalTransmitted { get; set; }
        public int TotalReceived { get; set; }

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            forceCloseTimer = new(OnForceCloseTimedEvent);
        }

        void TryShutdown(SocketShutdown how) {
            lock(this) {
                switch(how) {
                    case SocketShutdown.Receive:
                        shutdownReceive = true;
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
                        } catch(Exception) { }
                    }
                    clientsService.RemoveTcpClient(Port, OriginPort);
                    forceCloseTimer.Dispose();
                } else {
                    forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                }
            }
        }

        void OnForceCloseTimedEvent(object? state) {
            Debug.WriteLine($"tcp({localPort}) , o-port: {OriginPort}, attempt to close");
            TryShutdown(SocketShutdown.Both);
        }

        protected override void OnTimedEvent(object? state) { }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public override void Dispose() {
            socket.Close();
            base.Dispose();
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{TotalTransmitted}, rx:{TotalReceived}");
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
                TotalReceived += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();

                dataTunnelClient.PushOutgoingTcpData(new TcpStreamDataModel(Port, OriginPort, data), cancellationToken);

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }
            dataTunnelClient.PushOutgoingTcpData(new TcpStreamDataModel(Port, OriginPort, null), cancellationToken);
            TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendData(byte[]? data, CancellationToken cancellationToken) {
            if(data == null) {
                TryShutdown(SocketShutdown.Send);
                return;
            }

            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationToken);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
                _ = Task.Run(() => ReceivingStream(cancellationToken));
            }

            try {
                TotalTransmitted += await socket.SendAsync(data, SocketFlags.None, cancellationToken);
            } catch(SocketException) {
            } catch(ObjectDisposedException) {
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }

            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{data?.ToShortDescriptions()}");
            }

        }
    }
}
