using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.DataProtection;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {
    internal class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        protected readonly ConcurrentDictionary<int, Socket> remoteSockets = new();

        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger)
            : base(port, localEndPoint, dataTransferService, logger) {
            tcpServer = new TcpListener(localEndPoint.Address, port);

            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    try {
                        tcpServer.Start();
                        while(!cancellationToken.IsCancellationRequested) {
                            var socket = await tcpServer.AcceptSocketAsync(cancellationToken);

                            logger.Information($"tcp({port}) accept {socket.RemoteEndPoint}");
                            _ = Task.Run(async () => await HandleSocketAsync(socket, cancellationToken), cancellationToken);
                        }
                    } catch(Exception ex) {
                        logger.Error($"tcp({port}): {ex.Message}");
                    }
                    tcpServer.Stop();
                    logger.Information($"tcp({port}) close");
                }
            }, cancellationToken);
        }

        async Task HandleSocketAsync(Socket socket, CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            try {
                cancellationToken.Register(() => {
                    socket.Dispose();
                });
                while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                    var receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None);
                    if(receivedBytes == 0) {
                        break;
                    }
                    var data = receiveBuffer[..receivedBytes].ToArray();
                    await dataTransferService.SendTcpRequest(new TcpDataRequestModel(port, ((IPEndPoint)socket.RemoteEndPoint!).Port,
                        data));

                    remoteSockets.TryAdd(((IPEndPoint)socket.RemoteEndPoint!).Port, socket);

                    if(requestLogTimer <= DateTime.Now) {
                        requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"tcp({port}) request from {socket.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                    }
                }
            } catch(SocketException ex) {
                logger.Error($"tcp({port}) error from {socket.RemoteEndPoint}: {ex.Message}");
            } catch(OperationCanceledException ex) {
                logger.Error($"tcp({port}) error from {socket.RemoteEndPoint}: {ex.Message}");
            }
            await dataTransferService.SendTcpCommand(new TcpCommandModel(port, ((IPEndPoint)socket.RemoteEndPoint!).Port, SocketCommand.Disconnect));
            remoteSockets.TryRemove(((IPEndPoint)socket.RemoteEndPoint!).Port, out _);
        }

        public async Task SendResponse(TcpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested) {
                await dataTransferService.SendTcpCommand(new TcpCommandModel(port, response.OriginPort, SocketCommand.Disconnect));
                logger.Error($"tcp({port}) response to canceled {response.OriginPort}");
                return;
            }
            if(!remoteSockets.TryGetValue(response.OriginPort, out Socket? remoteSocket)) {
                await dataTransferService.SendTcpCommand(new TcpCommandModel(port, response.OriginPort, SocketCommand.Disconnect));
                logger.Error($"tcp({port}) response to missed {response.OriginPort}");
                return;
            }
            if(!remoteSocket.Connected) {
                await dataTransferService.SendTcpCommand(new TcpCommandModel(port, response.OriginPort, SocketCommand.Disconnect));
                logger.Error($"tcp({port}) response to disconnected {response.OriginPort}");
                return;
            }
            await remoteSocket.SendAsync(response.Data, SocketFlags.None, cancellationToken);

            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({port}) response to {remoteSocket.RemoteEndPoint}, bytes:{response.Data.ToShortDescriptions()}");
            }
        }

        public void Disconnect(TcpCommandModel command) {
            if(cancellationToken.IsCancellationRequested) {
                return;
            }
            if(!remoteSockets.TryRemove(command.OriginPort, out Socket? remoteSocket)) {
                return;
            }
            logger.Information($"tcp({port}) disconnect {remoteSocket.RemoteEndPoint}");
            if(!remoteSocket.Connected) {
                return;
            }
            remoteSocket.Shutdown(SocketShutdown.Both);
            remoteSocket.Close();
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
