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

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, CancellationTokenSource cancellationTokenSource)
            : base(serverEndPoint, originPort, logger, clientsService, cancellationTokenSource) {
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

        public async Task CreateStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream, ISignalRClient dataTunnelClient) {
            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationTokenSource.Token);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            }

            await dataTunnelClient.CreateStream(streamParam, ClientStreamData(), cancellationTokenSource.Token);

            await foreach(var data in stream.WithCancellation(cancellationTokenSource.Token)) {
                if(socket.Connected && !cancellationTokenSource.IsCancellationRequested) {
                    try {
                        TotalTransmitted += await socket.SendAsync(data, SocketFlags.None, cancellationTokenSource.Token);
                    } catch(SocketException) {
                    } catch(ObjectDisposedException) {
                    } catch(Exception ex) {
                        logger.Error(ex.GetBaseException().Message);
                    }
                }

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{data?.ToShortDescriptions()}");
                }
            }

            TryShutdown(SocketShutdown.Send);
        }

        async IAsyncEnumerable<byte[]> ClientStreamData() {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            while(socket.Connected && !cancellationTokenSource.IsCancellationRequested) {
                int receivedBytes;
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationTokenSource.Token);
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

                yield return data;

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }

            TryShutdown(SocketShutdown.Receive);
        }
    }
}
