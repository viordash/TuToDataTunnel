using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
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
        protected readonly ConcurrentDictionary<int, TcpClient> remoteClients = new();

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
                            var tcpClient = await tcpServer.AcceptTcpClientAsync(cancellationToken);

                            logger.Information($"tcp({port}) accept {tcpClient.Client.RemoteEndPoint}");
                            _ = Task.Run(async () => await HandleTcpClientAsync(tcpClient), cts.Token);
                        }

                        //while(!cancellationToken.IsCancellationRequested) {
                        //    var socket = await tcpServer.AcceptSocketAsync(cancellationToken);

                        //    logger.Information($"tcp({port}) accept {socket.RemoteEndPoint}");
                        //    _ = Task.Run(async () => await HandleSocketAsync(socket, cancellationToken), cancellationToken);
                        //}
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
            logger.Information($"tcp({port}) disconnected {socket.RemoteEndPoint}");
            socket.Dispose();
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



        async Task HandleTcpClientAsync(TcpClient tcpClient) {
            await dataTransferService.CreateTcpStream(new TcpStreamParam(port, ((IPEndPoint)tcpClient.Client.RemoteEndPoint!).Port));

            remoteClients.TryAdd(((IPEndPoint)tcpClient.Client.RemoteEndPoint!).Port, tcpClient);
        }

        public async IAsyncEnumerable<byte[]> AcceptStream(TcpStreamParam streamParam, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if(cancellationToken.IsCancellationRequested) {
                logger.Error($"tcp({port}) stream on canceled socket {streamParam.OriginPort}");
                yield break;
            }
            if(!remoteClients.TryGetValue(streamParam.OriginPort, out TcpClient? tcpClient)) {
                logger.Error($"tcp({port}) stream on missed socket {streamParam.OriginPort}");
                yield break;
            }
            if(!tcpClient.Connected) {
                logger.Error($"tcp({port}) stream on disconnected socket {streamParam.OriginPort}");
                yield break;
            }

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            int receivedBytes;
            int totalBytes = 0;
            while(tcpClient.Connected && !cancellationToken.IsCancellationRequested) {
                try {
                    receivedBytes = await tcpClient.Client.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException) {
                    break;
                }
                totalBytes += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();
                yield return data;

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {tcpClient.Client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }

            logger.Information($"tcp({port}) disconnected {tcpClient.Client.RemoteEndPoint}, transfered {totalBytes} b");
            remoteClients.TryRemove(((IPEndPoint)tcpClient.Client.RemoteEndPoint!).Port, out _);
            tcpClient.Dispose();
        }
    }
}
