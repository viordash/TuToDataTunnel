﻿using System.Collections.Concurrent;
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
    public class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource CancellationToken;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;


        #region inner classes
        protected class Client : IAsyncDisposable {
            public readonly Socket Socket;
            public readonly IPEndPoint RemoteEndPoint;
            readonly TcpServer parent;
            public readonly CancellationTokenSource CancellationToken;

            Int64 totalTransmitted;
            public Int64 TotalReceived;

            public Client(Socket socket, TcpServer parent) {
                this.parent = parent;
                Socket = socket;
                RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
                CancellationToken = new CancellationTokenSource();
            }

            public async ValueTask<bool> TryShutdown() {
                await DisposeAsync();
                return true;
            }

            public async ValueTask DisposeAsync() {
                if(parent.remoteSockets.TryRemove(RemoteEndPoint.Port, out _)) {

                    parent.logger.Information($"tcp({parent.port}) disconnected {RemoteEndPoint}, tx:{totalTransmitted}, rx:{TotalReceived}");
                    CancellationToken.Cancel();
                    try {
                        Socket.Shutdown(SocketShutdown.Both);
                    } catch(SocketException) { }
                    try {
                        await Socket.DisconnectAsync(true);
                    } catch(SocketException) { }
                    Socket.Close(100);
                }
                GC.SuppressFinalize(this);
            }

            public ValueTask<int> SendDataAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) {
                return Socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
            }

            public ValueTask<bool> DisconnectAsync() {
                return TryShutdown();
            }
        }
        #endregion

        protected readonly ConcurrentDictionary<int, Client> remoteSockets = new();

        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger)
            : base(port, localEndPoint, dataTransferService, logger) {
            tcpServer = new TcpListener(localEndPoint.Address, port);

            CancellationToken = new CancellationTokenSource();
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!CancellationToken.IsCancellationRequested) {
                    try {
                        tcpServer.Start();

                        while(!CancellationToken.IsCancellationRequested) {
                            var socket = await tcpServer.AcceptSocketAsync(CancellationToken.Token);

                            logger.Information($"tcp({port}) accept  {socket.RemoteEndPoint}");
                            var client = new Client(socket, this);
                            if(!remoteSockets.TryAdd(client.RemoteEndPoint.Port, client)) {
                                throw new TuToException($"tcp({port}) for {client.RemoteEndPoint} already exists");
                            }

                            bool clientConnected = await dataTransferService.ConnectTcp(new SocketAddressModel { Port = port, OriginPort = client.RemoteEndPoint.Port },
                                    CancellationToken.Token);

                            if(clientConnected) {
                                _ = Task.Run(() => HandleSocketAsync(client, CancellationToken.Token), CancellationToken.Token);
                            } else {
                                Dispose();
                            }
                        }
                    } catch(Exception ex) {
                        logger.Error($"tcp({port}): {ex.Message}");
                    }
                    tcpServer.Stop();
                    logger.Information($"tcp({port}) close");
                }
            }, CancellationToken.Token);
        }

        public ValueTask<bool> DisconnectAsync(SocketAddressModel socketAddress) {
            if(!remoteSockets.TryGetValue(socketAddress.OriginPort, out Client? client)) {
                return ValueTask.FromResult(false);
            }
            return client.DisconnectAsync();
        }

        public override async void Dispose() {
            CancellationToken.Cancel();
            foreach(var item in remoteSockets.Values.ToList()) {
                if(remoteSockets.TryGetValue(item.RemoteEndPoint.Port, out Client? client)) {
                    await client.DisposeAsync();
                }
            }
            GC.SuppressFinalize(this);
        }

        async Task HandleSocketAsync(Client client, CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, client.CancellationToken.Token);

            while(client.Socket.Connected && !cts.IsCancellationRequested) {
                try {
                    receivedBytes = await client.Socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);
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
                var transmitted = await dataTransferService.SendTcpRequest(new TcpDataRequestModel() { Port = port, OriginPort = client.RemoteEndPoint.Port, Data = data }, cts.Token);
                if(receivedBytes != transmitted) {
                    logger.Error($"tcp({port}) request from {client.RemoteEndPoint} send error ({transmitted})");
                }
                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) request from {client.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}");
                }
            }
            if(!client.CancellationToken.IsCancellationRequested) {
                if(!await dataTransferService.DisconnectTcp(new SocketAddressModel() { Port = port, OriginPort = client.RemoteEndPoint.Port },
                     cancellationToken)) {
                    logger.Error($"tcp({port}) request from {client.RemoteEndPoint} disconnect error");
                }
                await client.TryShutdown();
            }
        }

        public async ValueTask<int> SendResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
            if(!remoteSockets.TryGetValue(response.OriginPort, out Client? client)) {
                logger.Error($"tcp({port}) client stream on missed socket {response.OriginPort}");
                return -1;
            }
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, client.CancellationToken.Token, CancellationToken.Token);
            try {
                var transmitted = await client.SendDataAsync(response.Data, cts.Token);

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({port}) response to {client.RemoteEndPoint}, bytes:{response.Data.ToShortDescriptions()}");
                }
                return transmitted;
            } catch(SocketException) {
                return -3;
            } catch(ObjectDisposedException) {
                return -2;
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
                return -1;
            }
        }
    }
}
