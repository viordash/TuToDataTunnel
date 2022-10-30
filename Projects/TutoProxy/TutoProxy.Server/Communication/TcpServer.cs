using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {
    internal class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource cts;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        #region inner classes
        protected class CancelableClient {
            public readonly Socket Socket;
            public readonly CancellationTokenSource ReceiveCancellation;
            public readonly CancellationTokenSource TransmitCancellation;
            public readonly IPEndPoint RemoteEndPoint;
            readonly TcpServer parent;

            public int TotalTransmitted { get; set; }
            public int TotalReceived { get; set; }

            public CancelableClient(Socket socket, CancellationToken cancellationToken, TcpServer parent) {
                this.parent = parent;
                Socket = socket;
                RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
                ReceiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                TransmitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                ReceiveCancellation.Token.Register(TryShutdown);
                TransmitCancellation.Token.Register(TryShutdown);
            }

            void TryShutdown() {
                if(ReceiveCancellation.IsCancellationRequested && TransmitCancellation.IsCancellationRequested) {
                    parent.remoteSockets.TryRemove(RemoteEndPoint.Port, out _);
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close();
                    parent.logger.Information($"tcp({parent.port}) disconnected {RemoteEndPoint}, tx:{TotalTransmitted}, rx:{TotalReceived}");
                }

                if(!ReceiveCancellation.IsCancellationRequested) {
                    ReceiveCancellation.CancelAfter(TcpSocketParams.ReceiveTimeout);
                }

                if(!TransmitCancellation.IsCancellationRequested) {
                    TransmitCancellation.CancelAfter(TcpSocketParams.ReceiveTimeout);
                }
            }
        }
        #endregion

        protected readonly ConcurrentDictionary<int, CancelableClient> remoteSockets = new();

        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger)
            : base(port, localEndPoint, dataTransferService, logger) {
            tcpServer = new TcpListener(localEndPoint.Address, port);

            cts = new CancellationTokenSource();
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cts.IsCancellationRequested) {
                    try {
                        tcpServer.Start();

                        while(!cts.IsCancellationRequested) {
                            var socket = await tcpServer.AcceptSocketAsync(cts.Token);

                            if(cts.IsCancellationRequested) {
                                break;
                            }
                            logger.Information($"tcp({port}) accept {socket.RemoteEndPoint}");
                            _ = Task.Run(async () => await HandleSocketAsync(new CancelableClient(socket, cts.Token, this), cts.Token), cts.Token);
                        }
                    } catch(Exception ex) {
                        logger.Error($"tcp({port}): {ex.Message}");
                    }
                    tcpServer.Stop();
                    logger.Information($"tcp({port}) close");
                }
            }, cts.Token);
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
        }

        async Task HandleSocketAsync(CancelableClient cancelableClient, CancellationToken cancellationToken) {
            await dataTransferService.CreateTcpStream(new TcpStreamParam(port, cancelableClient.RemoteEndPoint.Port), cancellationToken);

            remoteSockets.TryAdd(((IPEndPoint)cancelableClient.RemoteEndPoint!).Port, cancelableClient);
        }

        public async IAsyncEnumerable<byte[]> CreateStream(TcpStreamParam streamParam, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out CancelableClient? cancelableClient)) {
                logger.Error($"tcp({port}) stream on missed socket {streamParam.OriginPort}");
                yield break;
            }

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;
            while(cancelableClient.Socket.Connected && !cancellationToken.IsCancellationRequested) {
                try {
                    receivedBytes = await cancelableClient.Socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        cancelableClient.ReceiveCancellation.Cancel();
                        break;
                    }
                } catch(OperationCanceledException) {
                    break;
                } catch(SocketException) {
                    break;
                }
                cancelableClient.TotalReceived += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();
                yield return data;

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {cancelableClient.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }

            if(cancellationToken.IsCancellationRequested && !cancelableClient.ReceiveCancellation.IsCancellationRequested) {
                cancelableClient.TransmitCancellation.Cancel();
            }
        }

        public async Task AcceptClientStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream) {
            if(cts.IsCancellationRequested) {
                logger.Error($"tcp({port}) client stream on canceled socket {streamParam.OriginPort}");
                return;
            }
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out CancelableClient? cancelableClient)) {
                logger.Error($"tcp({port}) client stream on missed socket {streamParam.OriginPort}");
                return;
            }
            if(!cancelableClient.Socket.Connected) {
                logger.Error($"tcp({port}) client stream on disconnected socket {streamParam.OriginPort}");
                return;
            }

            try {
                await foreach(var data in stream.WithCancellation(cancelableClient.ReceiveCancellation.Token)) {
                    cancelableClient.TotalTransmitted += await cancelableClient.Socket.SendAsync(data, SocketFlags.None, cancelableClient.TransmitCancellation.Token);
                    if(responseLogTimer <= DateTime.Now) {
                        responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"tcp({port}) response to {cancelableClient.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                    }
                }
                cancelableClient.ReceiveCancellation.Cancel();
            } catch(OperationCanceledException ex) {
                cancelableClient.TransmitCancellation.Cancel();
                //logger.Error(ex.GetBaseException().Message);
            } catch(SocketException) {
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }
        }
    }
}
