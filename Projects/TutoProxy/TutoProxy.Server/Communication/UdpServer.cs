using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Communication {
    public class UdpServer : BaseServer {
        readonly UdpClient udpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;

        public UdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IDateTimeService dateTimeService)
            : base(port, localEndPoint, dataTransferService, logger, dateTimeService) {
            udpServer = new UdpClient(port);
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    var result = await udpServer.ReceiveAsync(cancellationToken);
                    logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                    await dataTransferService.SendUdpRequest(new UdpDataRequestModel(port, result.RemoteEndPoint.Port, result.Buffer));
                    AddRemoteEndPoint(result.RemoteEndPoint, cancellationToken);
                }
            }, cancellationToken);
        }

        public async Task SendResponse(UdpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested) {
                return;
            }
            if(!remoteEndPoints.TryGetValue(response.RemotePort, out RemoteEndPoint? remoteEndPoint)) {
                return;
            }
            var txCount = await udpServer.SendAsync(response.Data, remoteEndPoint.EndPoint, cancellationToken);
            logger.Information($"udp response to {remoteEndPoint}, bytes:{txCount}");
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
            udpServer.Dispose();
        }
    }
}
