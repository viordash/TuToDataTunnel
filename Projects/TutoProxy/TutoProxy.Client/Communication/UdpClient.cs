using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {
    public class UdpClient : BaseClient {
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        bool connected = false;

        protected virtual TimeSpan ReceiveTimeout { get { return UdpSocketParams.ReceiveTimeout; } }
        public bool Listening { get; private set; } = false;
        protected readonly Timer timeoutTimer;
        readonly System.Net.Sockets.UdpClient socket;

        public UdpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {

            timeoutTimer = new(OnTimedEvent, null, ReceiveTimeout, Timeout.InfiniteTimeSpan);
            socket = new System.Net.Sockets.UdpClient(serverEndPoint.AddressFamily);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            socket.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            socket.ExclusiveAddressUse = false;
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, created");
        }

        protected void OnTimedEvent(object? state) {
            clientsService.RemoveUdpClient(Port, OriginPort);
        }

        public void Refresh() {
            if(!timeoutTimer.Change(ReceiveTimeout, Timeout.InfiniteTimeSpan)) {
                logger.Error($"udp: {serverEndPoint}, o-port: {OriginPort}, Refresh error");
            }
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var txCount = await socket!.SendAsync(payload, serverEndPoint, cancellationToken);
            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) request to {serverEndPoint}, bytes:{payload.ToShortDescriptions()}");
            }
        }

        public void Listen(UdpDataRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            if(Listening) {
                throw new TuToException($"udp 0, port: {request.Port}, o-port: {request.OriginPort}, already listening");
            }
            Listening = true;
            _ = Task.Run(async () => {
                try {
                    connected = true;
                    while(connected && !cancellationToken.IsCancellationRequested) {
                        var result = await socket.ReceiveAsync(cancellationToken);
                        if(result.Buffer.Length == 0) {
                            break;
                        }
                        var response = new UdpDataResponseModel() { Port = request.Port, OriginPort = request.OriginPort, Data = result.Buffer };
                        await dataTunnelClient.SendUdpResponse(response, cancellationToken);

                        if(responseLogTimer <= DateTime.Now) {
                            responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                            logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.ToShortDescriptions()}.");
                        }
                    };
                    Listening = false;
                    connected = false;
                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel() { Port = request.Port, OriginPort = request.OriginPort }, Int64.MinValue, cancellationToken);
                    logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) disconnected");
                } catch(SocketException ex) {
                    Listening = false;
                    connected = false;

                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel() { Port = request.Port, OriginPort = request.OriginPort }, Int64.MinValue, cancellationToken);
                    logger.Error($"udp socket: {ex.Message}");
                } catch {
                    Listening = false;
                    connected = false;
                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel() { Port = request.Port, OriginPort = request.OriginPort }, Int64.MinValue, cancellationToken);
                    throw;
                }
            });
        }

        public override void Dispose() {
            base.Dispose();
            connected = false;
            socket.Close();
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed");
        }

        public void Disconnect(Int64 transferLimit) {
            clientsService.RemoveUdpClient(Port, OriginPort);
        }
    }
}
