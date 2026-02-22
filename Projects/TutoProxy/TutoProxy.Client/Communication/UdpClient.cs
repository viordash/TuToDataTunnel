using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;
using TuToProxy.Core.Pooling;

namespace TutoProxy.Client.Communication {
    public class UdpClient : BaseClient {
        long requestLogTicks = Environment.TickCount64;
        long responseLogTicks = Environment.TickCount64;
        bool connected = false;

        protected virtual TimeSpan ReceiveTimeout { get { return UdpSocketParams.ReceiveTimeout; } }
        public bool Listening { get; private set; } = false;
        protected readonly Timer timeoutTimer;
        readonly System.Net.Sockets.UdpClient socket;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public UdpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient, IProcessMonitor processMonitor)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient, processMonitor) {

            timeoutTimer = new(OnTimedEvent, null, ReceiveTimeout, Timeout.InfiniteTimeSpan);
            socket = new System.Net.Sockets.UdpClient(serverEndPoint.AddressFamily);
            socket.ExclusiveAddressUse = false;
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, created");
        }

        protected void OnTimedEvent(object? state) {
            clientsService.RemoveUdpClient(Port, OriginPort);
        }

        public override string ToString() {
            return $"udp {base.ToString()}";
        }

        public void Refresh() {
            if(!timeoutTimer.Change(ReceiveTimeout, Timeout.InfiniteTimeSpan)) {
                logger.Error($"{this}, Refresh error");
            }
        }

        public async Task SendRequest(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) {
            var transmitted = await socket!.SendAsync(payload, serverEndPoint, cancellationToken);
            totalTransmitted += transmitted;
            if(UdpSocketParams.TrafficMonitoring && Environment.TickCount64 - requestLogTicks >= UdpSocketParams.LogUpdatePeriod * 1000) {
                requestLogTicks = Environment.TickCount64;
                logger.Information($"{this} request, bytes:{payload.ToShortDescriptions()}");
                processMonitor.UdpClientData(this, totalTransmitted, totalReceived);
            }
        }

        public void Listen(UdpDataRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            if(Listening) {
                throw new TuToException($"{this}, already listening");
            }
            Listening = true;
            _ = Task.Run(async () => {
                try {
                    connected = true;
                    processMonitor.ConnectUdpClient(this);
                    while(connected && !cancellationToken.IsCancellationRequested) {
                        var result = await socket.ReceiveAsync(cancellationToken);
                        if(result.Buffer.Length == 0) {
                            break;
                        }
                        totalReceived += result.Buffer.Length;
                        var response = DataModelPool<UdpDataResponseModel>.Rent();
                        response.Port = request.Port;
                        response.OriginPort = request.OriginPort;
                        response.Data = result.Buffer;
                        try {
                            await dataTunnelClient.SendUdpResponse(response, cancellationToken);
                        } finally {
                            DataModelPool<UdpDataResponseModel>.Return(response);
                        }

                        if(UdpSocketParams.TrafficMonitoring && Environment.TickCount64 - responseLogTicks >= UdpSocketParams.LogUpdatePeriod * 1000) {
                            responseLogTicks = Environment.TickCount64;
                            logger.Information($"{this} response, bytes:{result.Buffer.ToShortDescriptions()}.");
                            processMonitor.UdpClientData(this, totalTransmitted, totalReceived);
                        }
                    };
                    Listening = false;
                    connected = false;
                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel() { Port = request.Port, OriginPort = request.OriginPort }, Int64.MinValue, cancellationToken);
                    logger.Information($"{this} disconnected");
                } catch(SocketException ex) {
                    Listening = false;
                    connected = false;

                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel() { Port = request.Port, OriginPort = request.OriginPort }, Int64.MinValue, cancellationToken);
                    logger.Error($"{this} ex: {ex.Message}");
                } catch {
                    Listening = false;
                    connected = false;
                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel() { Port = request.Port, OriginPort = request.OriginPort }, Int64.MinValue, cancellationToken);
                    throw;
                }
            });
        }

        public override async ValueTask DisposeAsync() {
            cancellationTokenSource.Cancel();
            connected = false;
            socket.Close();
            processMonitor.DisconnectUdpClient(this);
            logger.Information($"{this}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
            await base.DisposeAsync();
        }

        public void Disconnect(Int64 transferLimit) {
            clientsService.RemoveUdpClient(Port, OriginPort);
        }
    }
}
