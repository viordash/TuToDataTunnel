//using System.Net;
//using System.Net.Sockets;
//using TutoProxy.Server.Services;

//namespace TutoProxy.Server.Communication {
//    internal class TcpServer : BaseServer {
//        readonly TcpListener tcpServer;
//        readonly CancellationTokenSource cts;
//        readonly CancellationToken cancellationToken;

//        IPEndPoint? remoteEndPoint = null;

//        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger)
//            : base(port, localEndPoint, dataTransferService, logger) {
//            tcpServer = new TcpListener(IPAddress.Loopback, port);

//            cts = new CancellationTokenSource();
//            cancellationToken = cts.Token;
//        }

//        public Task Listen() {
//            return Task.Run(async () => {

//                tcpServer.Start();
//                while(!cancellationToken.IsCancellationRequested) {

//                    var client = tcpServer.AcceptTcpClient();

//                    logger.Information($"tcp accept {client}");
//                    Task.Run(() => HandleClient(client));


//                    var result = await tcpServer.ReceiveAsync(cancellationToken);
//                    await dataTransferService.SendUdpRequest(new UdpDataRequestModel(port, result.Buffer));
//                    remoteEndPoint = result.RemoteEndPoint;
//                }
//            }, cancellationToken);
//        }

//        public async Task SendResponse(UdpDataResponseModel response) {
//            if(cancellationToken.IsCancellationRequested || remoteEndPoint == null) {
//                return;
//            }
//            var txCount = await tcpServer.SendAsync(response.Data, remoteEndPoint, cancellationToken);
//            logger.Information($"udp response to {remoteEndPoint}, bytes:{txCount}");
//        }

//        public override void Dispose() {
//            cts.Cancel();
//            cts.Dispose();
//            tcpServer.Dispose();
//        }
//    }
//}
