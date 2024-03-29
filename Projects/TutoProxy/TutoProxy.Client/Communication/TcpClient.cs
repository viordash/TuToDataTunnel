﻿using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {

    public class TcpClient : BaseClient {
        int? localPort = null;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly Socket socket;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient, IProcessMonitor processMonitor)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient, processMonitor) {

            socket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveBufferSize = TcpSocketParams.ReceiveBufferSize;
            socket.SendBufferSize = TcpSocketParams.ReceiveBufferSize;
            logger.Information($"{this}, created");
        }

        public override string ToString() {
            return $"tcp({localPort,5}) {base.ToString()}";
        }

        public override async ValueTask DisposeAsync() {
            cancellationTokenSource.Cancel();
            try {
                socket.Shutdown(SocketShutdown.Both);
            } catch(SocketException) { }
            try {
                await socket.DisconnectAsync(true);
            } catch(SocketException) { }
            socket.Close(100);
            processMonitor.DisconnectTcpClient(this);
            logger.Information($"{this}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
            await base.DisposeAsync();
        }

        async ValueTask<SocketError> ConnectInternal(CancellationToken cancellationToken, int nestedLevel) {
            if(socket.Connected) {
                logger.Error($"{this}, already connected");
                return SocketError.Success;
            }

            try {
                await socket.ConnectAsync(serverEndPoint);
                processMonitor.ConnectTcpClient(this);
            } catch(SocketException ex) {
                logger.Error($"{this}, connect socket ex: {ex.GetBaseException().Message}");
                return ex.SocketErrorCode;
            } catch(Exception ex) {
                logger.Error($"{this}, connect ex: {ex.GetBaseException().Message}");
                return SocketError.SocketError;
            }
            localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            _ = Task.Run(async () => await ReceivingStream(cancellationToken), cancellationToken);
            return SocketError.Success;
        }

        public ValueTask<SocketError> Connect(CancellationToken cancellationToken) {
            return ConnectInternal(cancellationToken, 0);
        }

        async Task ReceivingStream(CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);

            try {
                while(socket.Connected && !cts.IsCancellationRequested) {
                    int receivedBytes;
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);
                    if(receivedBytes == 0) {
                        break;
                    }

                    totalReceived += receivedBytes;
                    var data = receiveBuffer[..receivedBytes];

                    var response = new TcpDataResponseModel() { Port = Port, OriginPort = OriginPort, Data = data };
                    var transmitted = await dataTunnelClient.SendTcpResponse(response, cancellationToken);
                    if(receivedBytes != transmitted) {
                        logger.Error($"{this} response transmit error ({transmitted})");
                        throw new SocketException((int)SocketError.ConnectionAborted);
                    }
                    if(responseLogTimer <= DateTime.Now) {
                        responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"{this} response, bytes:{data.ToShortDescriptions()}.");
                        processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                    }
                }
            } catch(OperationCanceledException) {
            } catch(SocketException ex) {
                logger.Error($"{this} rx socket ex:{ex.GetBaseException().Message}");
            } catch(Exception ex) {
                logger.Error($"{this} rx ex:{ex.GetBaseException().Message}");
            }
            if(!cancellationTokenSource.IsCancellationRequested) {
                try {
                    if(!await dataTunnelClient.DisconnectTcp(new SocketAddressModel() { Port = Port, OriginPort = OriginPort }, cancellationToken)) {
                        logger.Error($"{this} disconnect command error");
                    }
                } catch(Exception) { }
                await DisconnectAsync();
            }
        }

        public async ValueTask<int> SendRequest(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            try {
                var transmitted = await socket.SendAsync(payload, SocketFlags.None, cts.Token);
                if(transmitted != payload.Length) {
                    logger.Error($"{this} request transmit error ({transmitted} != {payload.Length})");
                }
                totalTransmitted += transmitted;
                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"{this} request, bytes:{payload.ToShortDescriptions()}");
                    processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                }
                return transmitted;
            } catch(SocketException ex) {
                logger.Error($"{this} send socket ex:{ex.GetBaseException().Message}");
                return -3;
            } catch(ObjectDisposedException) {
                return -2;
            } catch(Exception ex) {
                logger.Error($"{this} send ex:{ex.GetBaseException().Message}");
                return -1;
            }
        }

        public ValueTask<bool> DisconnectAsync() {
            return clientsService.RemoveTcpClient(Port, OriginPort);
        }

    }
}
