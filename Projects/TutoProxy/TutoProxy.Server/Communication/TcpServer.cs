using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;
using TuToProxy.Core.Queue;

namespace TutoProxy.Server.Communication {
    internal class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource cts;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        #region inner classes
        protected class Client : IDisposable {
            const int firstFrame = 0;
            public readonly Socket Socket;
            public readonly IPEndPoint RemoteEndPoint;
            readonly TcpServer parent;
            readonly Timer forceCloseTimer;
            public readonly SortedQueue IncomingQueue;
            public bool shutdownReceive;
            public bool shutdownTransmit;
            Int64 frame;

            public Int64 TotalTransmitted { get; set; }
            public Int64 TotalReceived { get; set; }

            public Client(Socket socket, TcpServer parent) {
                this.parent = parent;
                Socket = socket;
                RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
                forceCloseTimer = new Timer(OnForceCloseTimedEvent);
                frame = firstFrame - 1;
                IncomingQueue = new SortedQueue(firstFrame, TcpSocketParams.QueueMaxSize);
            }

            public void TryShutdown(SocketShutdown how) {
                lock(this) {
                    switch(how) {
                        case SocketShutdown.Receive:
                            shutdownReceive = true;
                            try {
                                if(Socket.Connected) {
                                    Socket.Shutdown(SocketShutdown.Receive);
                                }
                            } catch(Exception) { }
                            break;
                        case SocketShutdown.Send:
                            shutdownTransmit = true;
                            try {
                                if(Socket.Connected) {
                                    Socket.Shutdown(SocketShutdown.Send);
                                }
                            } catch(Exception) { }
                            break;
                        case SocketShutdown.Both:
                            shutdownReceive = true;
                            shutdownTransmit = true;
                            break;
                    }


                    if(shutdownReceive && shutdownTransmit) {
                        parent.remoteSockets.TryRemove(RemoteEndPoint.Port, out _);
                        try {
                            Socket.Shutdown(SocketShutdown.Both);
                        } catch(SocketException) { }
                        Socket.Close();
                        Socket.Dispose();
                        forceCloseTimer.Dispose();
                        parent.logger.Information($"tcp({parent.port}) disconnected {RemoteEndPoint}, tx:{TotalTransmitted}, rx:{TotalReceived}");
                    } else {
                        forceCloseTimer.Change(TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(30));
                    }
                }
            }

            void OnForceCloseTimedEvent(object? state) {
                lock(this) {
                    if(!shutdownReceive || !shutdownTransmit) {
                        Debug.WriteLine($"Attempt to close: {parent.port}, {RemoteEndPoint.Port}");
                        TryShutdown(SocketShutdown.Both);
                    }
                }
            }

            public Int64 NextFrame() {
                return Interlocked.Increment(ref frame);
            }

            public void Dispose() {
                TryShutdown(SocketShutdown.Both);
                GC.SuppressFinalize(this);
            }
        }
        #endregion

        protected readonly ConcurrentDictionary<int, Client> remoteSockets = new();

        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, HubClient hubClient, ILogger logger)
            : base(port, localEndPoint, dataTransferService, hubClient, logger) {
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
                            _ = Task.Run(() => HandleSocketAsync(socket, cts.Token), cts.Token);
                        }
                    } catch(Exception ex) {
                        logger.Error($"tcp({port}): {ex.Message}");
                    }
                    tcpServer.Stop();
                    logger.Information($"tcp({port}) close");
                }
            }, cts.Token);
        }

        public void Disconnect(SocketAddressModel socketAddress) {
            if(!remoteSockets.TryGetValue(socketAddress.OriginPort, out Client? client)) {
                return;
            }
            client.TryShutdown(SocketShutdown.Send);
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
            foreach(var client in remoteSockets.Values) {
                client.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        async Task HandleSocketAsync(Socket socket, CancellationToken cancellationToken) {
            var client = new Client(socket, this);
            if(!remoteSockets.TryAdd(client.RemoteEndPoint.Port, client)) {
                throw new TuToException($"tcp({port}) for {client.RemoteEndPoint} already exists");
            }

            //client.Socket.LingerState = new LingerOption(true, 10);

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;
            while(client.Socket.Connected && !cancellationToken.IsCancellationRequested) {
                try {
                    receivedBytes = await client.Socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
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
                hubClient.PushOutgoingTcpData(new TcpStreamDataModel(port, client.RemoteEndPoint.Port, client.NextFrame(), data));

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }

            await dataTransferService.DisconnectTcp(new SocketAddressModel(port, client.RemoteEndPoint.Port), client.TotalReceived);

            client.TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendData(TcpStreamDataModel streamData) {
            if(!remoteSockets.TryGetValue(streamData.OriginPort, out Client? client)) {
                logger.Error($"tcp({port}) client stream on missed socket {streamData.OriginPort}");
                return;
            }

            client.IncomingQueue.Enqueue(streamData.Frame, streamData.Data);

            if(client.IncomingQueue.TryDequeue(out byte[]? orderedData)) {
                if(orderedData == null) {
                    client.TryShutdown(SocketShutdown.Send);
                    return;
                }

                try {
                    if(client.Socket.Connected) {
                        client.TotalTransmitted += await client.Socket.SendAsync(orderedData, SocketFlags.None, cts.Token);
                    }
                } catch(SocketException) {
                } catch(ObjectDisposedException) {
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                }

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) response to {client.RemoteEndPoint}, bytes:{orderedData.ToShortDescriptions()}");
                }
            }
        }
    }
}
