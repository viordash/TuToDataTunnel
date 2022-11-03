﻿using System.Collections.Concurrent;
using System.Diagnostics;
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
        protected class Client {
            public readonly Socket Socket;
            public readonly IPEndPoint RemoteEndPoint;
            readonly TcpServer parent;
            readonly Timer forceCloseTimer;
            bool shutdownReceive;
            bool shutdownTransmit;

            public int TotalTransmitted { get; set; }
            public int TotalReceived { get; set; }

            public Client(Socket socket, TcpServer parent) {
                this.parent = parent;
                Socket = socket;
                RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
                forceCloseTimer = new Timer(OnForceCloseTimedEvent);
            }

            public void TryShutdown(SocketShutdown how) {
                lock(this) {
                    switch(how) {
                        case SocketShutdown.Receive:
                            shutdownReceive = true;
                            break;
                        case SocketShutdown.Send:
                            shutdownTransmit = true;
                            if(Socket.Connected) {
                                try {
                                    Socket.Shutdown(SocketShutdown.Send);
                                } catch(Exception) { }
                            }
                            break;
                        case SocketShutdown.Both:
                            shutdownReceive = true;
                            shutdownTransmit = true;
                            break;
                    }


                    if(shutdownReceive && shutdownTransmit) {
                        parent.remoteSockets.TryRemove(RemoteEndPoint.Port, out _);
                        if(Socket.Connected) {
                            try {
                                Socket.Shutdown(SocketShutdown.Both);
                            } catch(Exception) { }
                        }
                        Socket.Close();
                        Socket.Dispose();
                        forceCloseTimer.Dispose();
                        parent.logger.Information($"tcp({parent.port}) disconnected {RemoteEndPoint}, tx:{TotalTransmitted}, rx:{TotalReceived}");
                    } else {
                        forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                    }
                }
            }

            void OnForceCloseTimedEvent(object? state) {
                Debug.WriteLine($"Attempt to close: {parent.port}, {RemoteEndPoint.Port}");
                TryShutdown(SocketShutdown.Both);
            }
        }
        #endregion

        protected readonly ConcurrentDictionary<int, Client> remoteSockets = new();

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
                            _ = Task.Run(async () => await HandleSocketAsync(new Client(socket, this), cts.Token), cts.Token);
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

        async Task HandleSocketAsync(Client client, CancellationToken cancellationToken) {
            await dataTransferService.CreateTcpStream(new TcpStreamParam(port, client.RemoteEndPoint.Port), cancellationToken);

            remoteSockets.TryAdd(client.RemoteEndPoint.Port, client);

            //Debug.WriteLine($"tcp server, ------------------ HandleSocketAsync {remoteSockets.Count}");
        }

        public async IAsyncEnumerable<byte[]> CreateStream(TcpStreamParam streamParam, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out Client? client)) {
                logger.Error($"tcp({port}) stream on missed socket {streamParam.OriginPort}");
                yield break;
            }

            var coopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;
            while(client.Socket.Connected && !coopCts.IsCancellationRequested) {
                try {
                    receivedBytes = await client.Socket.ReceiveAsync(receiveBuffer, SocketFlags.None, coopCts.Token);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException) {
                    break;
                } catch(SocketException) {
                    break;
                }
                client.TotalReceived += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();

                yield return data;

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }
            client.TryShutdown(SocketShutdown.Receive);
        }

        public async Task AcceptClientStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream) {
            if(cts.IsCancellationRequested) {
                logger.Error($"tcp({port}) client stream on canceled socket {streamParam.OriginPort}");
                return;
            }
            if(!remoteSockets.TryGetValue(streamParam.OriginPort, out Client? client)) {
                logger.Error($"tcp({port}) client stream on missed socket {streamParam.OriginPort}");
                return;
            }
            if(!client.Socket.Connected) {
                logger.Error($"tcp({port}) client stream on disconnected socket {streamParam.OriginPort}");
                return;
            }

            try {
                await foreach(var data in stream) {
                    if(client.Socket.Connected && !cts.IsCancellationRequested) {
                        try {
                            client.TotalTransmitted += await client.Socket.SendAsync(data, SocketFlags.None, cts.Token);
                        } catch(SocketException) {
                        } catch(ObjectDisposedException) {
                        }
                    }

                    if(responseLogTimer <= DateTime.Now) {
                        responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"tcp({port}) response to {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                    }
                }

            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }

            client.TryShutdown(SocketShutdown.Send);
        }
    }
}
