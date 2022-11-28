using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {
    public class TcpServer : BaseServer {
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
                    if(parent.remoteSockets.TryRemove(RemoteEndPoint.Port, out Client? client)) {
                        parent.logger.Information($"tcp({parent.port}) disconnected {RemoteEndPoint}, tx:{totalTransmitted}, rx:{TotalReceived}");
                        client.Dispose();
                    }
                } else {
                    StartClosingTimer();
                }
            }

            void OnForceCloseTimedEvent(object? state) {
                Debug.WriteLine($"Attempt to close: {parent.port}, {RemoteEndPoint.Port}");
                TryShutdown(SocketShutdown.Both);
            }

            void StartClosingTimer() {
                forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }

            public async void Dispose() {
                try {
                    Socket.Shutdown(SocketShutdown.Both);
                } catch(SocketException) { }
                try {
                    await Socket.DisconnectAsync(true);
                } catch(SocketException) { }
                forceCloseTimer.Dispose();
                Socket.Close(100);
                Socket.Dispose();
                GC.SuppressFinalize(this);
            }

            public async Task SendDataAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) {
                var transmitted = Interlocked.Add(ref totalTransmitted, await Socket.SendAsync(buffer, SocketFlags.None, cancellationToken));

                var limit = Interlocked.Read(ref limitTotalTransmitted);
                if(limit >= 0) {
                    if(transmitted >= limit) {
                        TryShutdown(SocketShutdown.Send);
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

                            logger.Information($"tcp({port}) accept  {socket.RemoteEndPoint}");
                            var client = new Client(socket, this);
                            if(!remoteSockets.TryAdd(client.RemoteEndPoint.Port, client)) {
                                throw new TuToException($"tcp({port}) for {client.RemoteEndPoint} already exists");
                            }

                            bool clientConnected = await dataTransferService.ConnectTcp(new SocketAddressModel { Port = port, OriginPort = client.RemoteEndPoint.Port },
                                    cts.Token);

                            if(clientConnected) {
                                _ = HandleSocketAsync(client, cts.Token);
                            } else {
                                client.Dispose();
                            }
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

        async Task HandleSocketAsync(Client client, CancellationToken cancellationToken) {
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
                await dataTransferService.SendTcpRequest(new TcpDataRequestModel() { Port = port, OriginPort = client.RemoteEndPoint.Port, Data = data });

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }

            await dataTransferService.DisconnectTcp(new SocketAddressModel() { Port = port, OriginPort = client.RemoteEndPoint.Port }, client.TotalReceived);

            client.TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendResponse(TcpDataResponseModel response) {
            if(!remoteSockets.TryGetValue(response.OriginPort, out Client? client)) {
                logger.Error($"tcp({port}) client stream on missed socket {response.OriginPort}");
                return;
            }
            try {
                await client.SendDataAsync(response.Data, cts.Token);
            } catch(SocketException) {
                client.TryShutdown(SocketShutdown.Send);
            } catch(ObjectDisposedException) {
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }

            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({port}) response to {client.RemoteEndPoint}, bytes:{response.Data.ToShortDescriptions()}");
            }
        }
    }
}
