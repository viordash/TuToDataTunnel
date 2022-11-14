using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;
using static TutoProxy.Server.Communication.UdpServer;

namespace TutoProxy.Server.Communication {
    internal class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource cts;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        #region inner classes
        protected class Client : IDisposable {
            public readonly Socket Socket;
            public readonly IPEndPoint RemoteEndPoint;
            readonly TcpServer parent;
            readonly Timer forceCloseTimer;
            public bool shutdownReceive;
            public bool shutdownTransmit;

            Int64 totalTransmitted;
            Int64 limitTotalTransmitted;
            public Int64 TotalReceived;

            public Client(Socket socket, TcpServer parent) {
                this.parent = parent;
                Socket = socket;
                RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
                forceCloseTimer = new Timer(OnForceCloseTimedEvent);
                limitTotalTransmitted = -1;
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
                        if(Socket.Connected) {
                            try {
                                Socket.Shutdown(SocketShutdown.Both);
                            } catch(SocketException) { }
                            try {
                                Socket.Disconnect(true);
                            } catch(SocketException) { }
                        }
                        Socket.Close(100);
                        Socket.Dispose();
                        forceCloseTimer.Dispose();
                        parent.logger.Information($"tcp({parent.port}) disconnected {RemoteEndPoint}, tx:{totalTransmitted}, rx:{TotalReceived}");
                    } else {
                        StartClosingTimer();
                    }
                }
            }

            void OnForceCloseTimedEvent(object? state) {
                Debug.WriteLine($"Attempt to close: {parent.port}, {RemoteEndPoint.Port}");
                TryShutdown(SocketShutdown.Both);
            }

            void StartClosingTimer() {
                forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }

            public void Dispose() {
                TryShutdown(SocketShutdown.Both);
                GC.SuppressFinalize(this);
            }

            public async Task SendDataAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) {
                bool connected = Socket.Connected;
                var transmitted = Interlocked.Add(ref totalTransmitted, await Socket.SendAsync(buffer, SocketFlags.None, cancellationToken));
                if(!connected) {
                    TryShutdown(SocketShutdown.Send);
                } else {
                    var limit = Interlocked.Read(ref limitTotalTransmitted);
                    if(limit >= 0) {
                        if(transmitted >= limit) {
                            TryShutdown(SocketShutdown.Send);
                        }
                    }
                }
            }

            public void Disconnect(Int64 transferLimit) {
                if(Interlocked.Read(ref totalTransmitted) >= transferLimit) {
                    TryShutdown(SocketShutdown.Both);
                } else {
                    Interlocked.Exchange(ref limitTotalTransmitted, transferLimit);
                    StartClosingTimer();
                }
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

        public void Disconnect(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(!remoteSockets.TryGetValue(socketAddress.OriginPort, out Client? client)) {
                return;
            }

            client.Disconnect(totalTransfered);
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
            foreach(var client in remoteSockets.Values) {
                client.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        async Task HandleSocketAsync1(Socket socket, CancellationToken cancellationToken) {
            var client = new Client(socket, this);
            if(!remoteSockets.TryAdd(client.RemoteEndPoint.Port, client)) {
                throw new TuToException($"tcp({port}) for {client.RemoteEndPoint} already exists");
            }

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
                hubClient.PushOutgoingTcpData(new TcpStreamDataModel(port, client.RemoteEndPoint.Port, 0, data));

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

            try {
                await client.SendDataAsync(streamData.Data, cts.Token);
            } catch(SocketException) {
                client.TryShutdown(SocketShutdown.Send);
            } catch(ObjectDisposedException) {
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }

            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({port}) response to {client.RemoteEndPoint}, bytes:{streamData.Data.ToShortDescriptions()}");
            }
        }



        public async Task SendResponse(TcpDataResponseModel response) {
            if(!remoteSockets.TryGetValue(response.OriginPort, out Client? client)) {
                await dataTransferService.DisconnectUdp(new SocketAddressModel(port, response.OriginPort), Int64.MinValue);
                logger.Error($"tcp({port}) response to missed {response.OriginPort}");
                return;
            }
            await client.Socket.SendAsync(response.Data, SocketFlags.None, cts.Token);
            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp response to {client.RemoteEndPoint}, bytes:{response.Data?.ToShortDescriptions()}");
            }
        }


        async Task HandleSocketAsync(Socket socket, CancellationToken cancellationToken) {
            var client = new Client(socket, this);
            if(!remoteSockets.TryAdd(client.RemoteEndPoint.Port, client)) {
                throw new TuToException($"tcp({port}) for {client.RemoteEndPoint} already exists");
            }

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

                await dataTransferService.SendTcpRequest(new TcpDataRequestModel(port, client.RemoteEndPoint.Port, data));

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }

            await dataTransferService.DisconnectTcp(new SocketAddressModel(port, client.RemoteEndPoint.Port), client.TotalReceived);

            client.TryShutdown(SocketShutdown.Receive);
        }
    }
}
