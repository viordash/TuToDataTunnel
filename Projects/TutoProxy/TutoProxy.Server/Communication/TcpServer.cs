using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
            public readonly CancellationTokenSource Cts;
            public readonly EndPoint? RemoteEndPoint;

            public CancelableClient(Socket socket, CancellationToken cancellationToken) {
                Socket = socket;
                RemoteEndPoint = socket.RemoteEndPoint;
                Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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

                            logger.Information($"tcp({port}) accept {socket.RemoteEndPoint}");
                            _ = Task.Run(async () => await HandleSocketAsync(new CancelableClient(socket, cts.Token), cts.Token), cts.Token);
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
            await dataTransferService.CreateTcpStream(new TcpStreamParam(port, ((IPEndPoint)cancelableClient.RemoteEndPoint!).Port), cancellationToken);

            remoteSockets.TryAdd(((IPEndPoint)cancelableClient.RemoteEndPoint!).Port, cancelableClient);
        }

        public async IAsyncEnumerable<byte[]> CreateStream(TcpStreamParam streamParam, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out CancelableClient? cancelableClient)) {
                logger.Error($"tcp({port}) stream on missed socket {streamParam.OriginPort}");
                yield break;
            }
            if(!cancelableClient.Socket.Connected) {
                logger.Error($"tcp({port}) stream on disconnected socket {streamParam.OriginPort}");
                yield break;
            }

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;
            int totalBytes = 0;
            while(cancelableClient.Socket.Connected && !cancellationToken.IsCancellationRequested) {
                try {
                    receivedBytes = await cancelableClient.Socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException) {
                    break;
                } catch(SocketException) {
                    break;
                }
                totalBytes += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();
                yield return data;

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {cancelableClient.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }
            logger.Information($"tcp({port}) disconnected {cancelableClient.RemoteEndPoint}, transfered {totalBytes} b");
            remoteSockets.TryRemove(((IPEndPoint)cancelableClient.RemoteEndPoint!).Port, out _);
            cancelableClient.Cts.Cancel();
            cancelableClient.Socket.Dispose();
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
                await foreach(var data in stream.WithCancellation(cancelableClient.Cts.Token)) {
                    await cancelableClient.Socket.SendAsync(data, SocketFlags.None, cancelableClient.Cts.Token);
                    if(responseLogTimer <= DateTime.Now) {
                        responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"tcp({port}) response to {cancelableClient.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                    }
                }
            } catch(OperationCanceledException) {
            } catch(SocketException) {

            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }
        }
    }
}
