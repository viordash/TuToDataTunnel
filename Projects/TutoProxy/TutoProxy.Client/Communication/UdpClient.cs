using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {
    public class UdpClient : BaseClient<System.Net.Sockets.UdpClient> {
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        bool connected = false;

        protected override TimeSpan ReceiveTimeout { get { return UdpSocketParams.ReceiveTimeout; } }
        public bool Listening { get; private set; } = false;

        public UdpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
        }

        protected override void OnTimedEvent(object? state) {
            clientsService.RemoveUdpClient(Port, OriginPort);
        }

        protected override System.Net.Sockets.UdpClient CreateSocket() {
            var udpClient = new System.Net.Sockets.UdpClient(serverEndPoint.AddressFamily);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, created");
            return udpClient;
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var txCount = await socket!.SendAsync(payload, serverEndPoint, cancellationToken);
            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) request to {serverEndPoint}, bytes:{payload.ToShortDescriptions()}");
            }
        }

        public void Listen(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            if(Listening) {
                throw new TuToException($"udp 0, port: {request.Payload.Port}, o-port: {request.Payload.OriginPort}, already listening");
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
                        var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.OriginPort,
                                result.Buffer));
                        await dataTunnelClient.SendUdpResponse(transferResponse, cancellationToken);

                        if(responseLogTimer <= DateTime.Now) {
                            responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                            logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.ToShortDescriptions()}.");
                        }
                    };
                    Listening = false;
                    connected = false;
                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel(request.Payload.Port, request.Payload.OriginPort), Int64.MinValue, cancellationToken);
                    logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) disconnected");
                } catch(SocketException ex) {
                    Listening = false;
                    connected = false;

                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel(request.Payload.Port, request.Payload.OriginPort), Int64.MinValue, cancellationToken);
                    logger.Error($"udp socket: {ex.Message}");
                } catch {
                    Listening = false;
                    connected = false;
                    await dataTunnelClient.DisconnectUdp(new SocketAddressModel(request.Payload.Port, request.Payload.OriginPort), Int64.MinValue, cancellationToken);
                    throw;
                }
            });
        }

        public override void Dispose() {
            connected = false;
            base.Dispose();
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed");
        }
    }
}
