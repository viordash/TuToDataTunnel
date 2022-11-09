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
            bool shutdownReceive;
            bool shutdownTransmit;
            Int64 frame;

            public int TotalTransmitted { get; set; }
            public int TotalReceived { get; set; }

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
                        StartClosingTimer();
                    }
                }
            }

            public void StartClosingTimer() {
                forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }

            public void StopClosingTimer() {
                forceCloseTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            void OnForceCloseTimedEvent(object? state) {
                Debug.WriteLine($"Attempt to close: {parent.port}, {RemoteEndPoint.Port}");
                TryShutdown(SocketShutdown.Both);
            }

            public Int64 NextFrame() {
                return Interlocked.Increment(ref frame);
            }
            public Int64 LastFrame() {
                return Interlocked.Read(ref frame);
            }

            public void Dispose() {
                TryShutdown(SocketShutdown.Both);
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
            foreach(var client in remoteSockets.Values) {
                client.Dispose();
            }
        }

        async Task HandleSocketAsync(Client client, CancellationToken cancellationToken) {
            if(!remoteSockets.TryAdd(client.RemoteEndPoint.Port, client)) {
                throw new TuToException($"tcp({port}) for {client.RemoteEndPoint} already exists");
            }

            bool remoteClosed = true;
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
                remoteClosed = false;
                hubClient.PushOutgoingTcpData(new TcpStreamDataModel(port, client.RemoteEndPoint.Port, client.NextFrame(), data));

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }
            if(!remoteClosed) {
                hubClient.PushOutgoingTcpData(new TcpStreamDataModel(port, client.RemoteEndPoint.Port, client.LastFrame(), null));
            }
            client.TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendData(TcpStreamDataModel streamData) {
            if(!remoteSockets.TryGetValue(streamData.OriginPort, out Client? client)) {
                logger.Error($"tcp({port}) client stream on missed socket {streamData.OriginPort}");
                return;
            }


            if(streamData.Data == null) {
                client.StartClosingTimer();
                if(client.IncomingQueue.FrameWasTakenLast(streamData.Frame)) {
                    client.TryShutdown(SocketShutdown.Send);
                } else {
                    logger.Information($"tcp({port}) response to {client.RemoteEndPoint}, not all data yet");
                }
                return;
            }

            client.IncomingQueue.Enqueue(streamData.Frame, streamData.Data);

            if(client.IncomingQueue.TryDequeue(out byte[]? orderedData)) {
                try {
                    client.TotalTransmitted += await client.Socket.SendAsync(orderedData, SocketFlags.None, cts.Token);
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
