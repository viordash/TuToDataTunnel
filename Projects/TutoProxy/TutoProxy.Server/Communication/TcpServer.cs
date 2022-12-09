using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Terminal.Gui;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Communication {
    public class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource CancellationToken;

        protected readonly ConcurrentDictionary<int, TcpClient> remoteSockets = new();

        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor)
            : base(port, localEndPoint, dataTransferService, logger, processMonitor) {
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

                            logger.Information($"tcp({Port}) accept  {socket.RemoteEndPoint}");

                            var socketAddress = new SocketAddressModel { Port = Port, OriginPort = ((IPEndPoint)socket.RemoteEndPoint!).Port };
                            var client = new TcpClient(socket, this, dataTransferService, logger, processMonitor);
                            if(!remoteSockets.TryAdd(socketAddress.OriginPort, client)) {
                                logger.Warning($"tcp({Port}): {client} already exists, try to remove");
                                if(await DisconnectAsync(socketAddress)) {
                                    logger.Warning($"tcp({Port}): {client} already exists, removed");
                                }
                                if(!remoteSockets.TryAdd(socketAddress.OriginPort, client)) {
                                    throw new TuToException($"{client} already exists");
                                }
                            }

                            bool clientConnected = await dataTransferService.ConnectTcp(socketAddress, CancellationToken.Token);
                            if(clientConnected) {
                                var receivingAction = async () => await client.ReceivingStream(CancellationToken.Token);
                                _ = Task.Run(receivingAction, CancellationToken.Token);
                            } else {
                                logger.Error($"tcp({Port}) not connected {socket.RemoteEndPoint}");
                                await DisconnectAsync(socketAddress);
                            }
                        }
                    } catch(Exception ex) {
                        logger.Error($"tcp({Port}): {ex.Message}");
                    }
                    tcpServer.Stop();
                    logger.Information($"tcp({Port}) close");
                }
            }, CancellationToken.Token);
        }

        public async ValueTask<bool> DisconnectAsync(SocketAddressModel socketAddress) {
            if(remoteSockets.TryRemove(socketAddress.OriginPort, out TcpClient? client)) {
                await client.DisposeAsync();
                return true;
            }
            return false;
        }

        public override async void Dispose() {
            CancellationToken.Cancel();
            foreach(var item in remoteSockets.Values.ToList()) {
                if(remoteSockets.TryRemove(item.OriginPort, out TcpClient? client)) {
                    await client.DisposeAsync();
                }
            }
            GC.SuppressFinalize(this);
        }

        public async ValueTask<int> SendResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
            if(!remoteSockets.TryGetValue(response.OriginPort, out TcpClient? client)) {
                logger.Error($"tcp({Port}) client stream on missed socket {response.OriginPort}");
                return -1;
            }

            return await client.SendDataAsync(response.Data, cancellationToken);
        }
    }
}
