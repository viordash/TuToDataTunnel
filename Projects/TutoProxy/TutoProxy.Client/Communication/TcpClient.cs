using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient<Socket> {
        int localPort;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly CancellationTokenSource receiveCancellation;
        readonly CancellationTokenSource transmitCancellation;
        public int TotalTransmitted { get; set; }
        public int TotalReceived { get; set; }

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, CancellationTokenSource cancellationTokenSource)
            : base(serverEndPoint, originPort, logger, clientsService, cancellationTokenSource) {
            receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            transmitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);

            receiveCancellation.Token.Register(TryShutdown);
            transmitCancellation.Token.Register(TryShutdown);
        }


        void TryShutdown() {
            if(receiveCancellation.IsCancellationRequested && transmitCancellation.IsCancellationRequested) {
                cancellationTokenSource.Cancel();
                clientsService.RemoveTcpClient(Port, OriginPort);
            }

            if(!receiveCancellation.IsCancellationRequested) {
                receiveCancellation.CancelAfter(TcpSocketParams.ReceiveTimeout);
            }

            if(!transmitCancellation.IsCancellationRequested) {
                transmitCancellation.CancelAfter(TcpSocketParams.ReceiveTimeout);
            }
        }

        protected override void OnTimedEvent(object? state) {
            //logger.Information($"tcp OnTimedEvent {Port} {OriginPort}");
            //clientsService.RemoveTcpClient(Port, OriginPort);
        }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public override void Dispose() {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            base.Dispose();
            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{TotalTransmitted}, rx:{TotalReceived}");
        }

        public async Task CreateStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream, ISignalRClient dataTunnelClient) {
            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationTokenSource.Token);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            }

            await dataTunnelClient.CreateStream(streamParam, ClientStreamData(), cancellationTokenSource.Token);

            try {
                await foreach(var data in stream.WithCancellation(receiveCancellation.Token)) {
                    if(!socket.Connected) {
                        logger.Information($"tcp({localPort}) request to {serverEndPoint}, ---- {cancellationTokenSource.Token.IsCancellationRequested}");
                        logger.Information($"tcp({localPort}) request to {serverEndPoint}, 444 {cancellationTokenSource.Token.IsCancellationRequested}");
                        //break;
                    }
                    TotalTransmitted += await socket.SendAsync(data, SocketFlags.None, transmitCancellation.Token);

                    if(requestLogTimer <= DateTime.Now) {
                        requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{data?.ToShortDescriptions()}");
                    }
                }
                receiveCancellation.Cancel();
            } catch(OperationCanceledException ex) {
                //logger.Error(ex.GetBaseException().Message);
                transmitCancellation.Cancel();
            } catch(SocketException) {

            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }
            //logger.Information($"tcp({localPort}) transmit to {serverEndPoint} stopped, total {totalBytes} b");
        }

        async IAsyncEnumerable<byte[]> ClientStreamData([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                int receivedBytes;
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        receiveCancellation.Cancel();
                        break;
                    }
                } catch(OperationCanceledException) {
                    break;
                } catch(SocketException) {
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
            if(cancellationToken.IsCancellationRequested && !receiveCancellation.IsCancellationRequested) {
                transmitCancellation.Cancel();
            }
        }
    }
}
