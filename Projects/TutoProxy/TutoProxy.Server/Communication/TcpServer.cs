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
using TuToProxy.Core.Exceptions;
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

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
        }

        async Task HandleSocketAsync(Socket socket, CancellationToken cancellationToken) {
            await dataTransferService.CreateTcpStream(new TcpStreamParam(port, ((IPEndPoint)socket.RemoteEndPoint!).Port), cancellationToken);

            remoteSockets.TryAdd(((IPEndPoint)socket.RemoteEndPoint!).Port, socket);
        }

        public async IAsyncEnumerable<byte[]> CreateStream(TcpStreamParam streamParam, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out Socket? socket)) {
                logger.Error($"tcp({port}) stream on missed socket {streamParam.OriginPort}");
                yield break;
            }
            if(!socket.Connected) {
                logger.Error($"tcp({port}) stream on disconnected socket {streamParam.OriginPort}");
                yield break;
            }

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;
            int totalBytes = 0;
            while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
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
                    logger.Information($"tcp({port}) request from {socket.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }
            logger.Information($"tcp({port}) disconnected {socket.RemoteEndPoint}, transfered {totalBytes} b");
            remoteSockets.TryRemove(((IPEndPoint)socket.RemoteEndPoint!).Port, out _);
            socket.Dispose();
        }

        public async Task AcceptClientStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream) {
            if(cancellationToken.IsCancellationRequested) {
                logger.Error($"tcp({port}) client stream on canceled socket {streamParam.OriginPort}");
                return;
            }
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out Socket? socket)) {
                logger.Error($"tcp({port}) client stream on missed socket {streamParam.OriginPort}");
                return;
            }
            if(!socket.Connected) {
                logger.Error($"tcp({port}) client stream on disconnected socket {streamParam.OriginPort}");
                return;
            }

            try {
                await foreach(var data in stream.WithCancellation(cancellationToken)) {
                    await socket.SendAsync(data, SocketFlags.None, cancellationToken);
                    if(responseLogTimer <= DateTime.Now) {
                        responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"tcp({port}) response to {socket.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                    }
                }
            } catch(Exception ex) {
                logger.Error(ex.ToString());
            }
        }
    }
}
