using System.Buffers;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;
using TuToProxy.Core.Pooling;

namespace TutoProxy.Client.Communication {

    public class TcpClient : BaseClient {
        int? localPort = null;
        long requestLogTicks = Environment.TickCount64;
        long responseLogTicks = Environment.TickCount64;
        readonly Socket socket;
        readonly ITcpDataChannel tcpChannel;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient, ITcpDataChannel tcpChannel, IProcessMonitor processMonitor)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient, processMonitor) {

            this.tcpChannel = tcpChannel;
            socket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
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
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(TcpSocketParams.ReceiveBufferSize);
            Memory<byte> receiveBuffer = rentedBuffer.AsMemory(0, TcpSocketParams.ReceiveBufferSize);

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

                    var response = DataModelPool<TcpDataResponseModel>.Rent();
                    response.Port = Port;
                    response.OriginPort = OriginPort;
                    response.Data = data;
                    int transmitted;
                    try {
                        transmitted = await tcpChannel.SendTcpResponse(response, cancellationToken);
                    } finally {
                        DataModelPool<TcpDataResponseModel>.Return(response);
                    }
                    if(receivedBytes != transmitted) {
                        logger.Error($"{this} response transmit error ({transmitted})");
                        throw new SocketException((int)SocketError.ConnectionAborted);
                    }
                    if(TcpSocketParams.TrafficMonitoring && Environment.TickCount64 - responseLogTicks >= TcpSocketParams.LogUpdatePeriod * 1000) {
                        responseLogTicks = Environment.TickCount64;
                        logger.Information($"{this} response, bytes:{data.ToShortDescriptions()}.");
                        processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                    }
                }
            } catch(OperationCanceledException) {
            } catch(SocketException ex) {
                logger.Error($"{this} rx socket ex:{ex.GetBaseException().Message}");
            } catch(Exception ex) {
                logger.Error($"{this} rx ex:{ex.GetBaseException().Message}");
            } finally {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            if(!cancellationTokenSource.IsCancellationRequested) {
                try {
                    if(!await tcpChannel.DisconnectTcp(new SocketAddressModel() { Port = Port, OriginPort = OriginPort }, cancellationToken)) {
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
                if(TcpSocketParams.TrafficMonitoring && Environment.TickCount64 - requestLogTicks >= TcpSocketParams.LogUpdatePeriod * 1000) {
                    requestLogTicks = Environment.TickCount64;
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
