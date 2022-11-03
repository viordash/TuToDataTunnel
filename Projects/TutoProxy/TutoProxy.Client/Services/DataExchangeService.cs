using System.Runtime.CompilerServices;
using TutoProxy.Client.Communication;

namespace TutoProxy.Client.Services {
    public interface IDataExchangeService {
        Task HandleUdpRequest(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationTokenSource cts);
        Task HandleUdpCommand(TransferUdpCommandModel command, ISignalRClient dataTunnelClient, CancellationToken cancellationToken);

        Task CreateStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream, ISignalRClient dataTunnelClient, CancellationTokenSource cts);

        Task AcceptIncomingDataStream(IAsyncEnumerable<TcpStreamDataModel> stream, CancellationToken cancellationToken);
        IAsyncEnumerable<TcpStreamDataModel> GetOutcomingDataStream(CancellationToken cancellationToken);
    }

    internal class DataExchangeService : IDataExchangeService {
        readonly ILogger logger;
        readonly IClientsService clientsService;

        public DataExchangeService(
            ILogger logger,
            IClientsService clientsService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.clientsService = clientsService;
        }

        public async Task HandleUdpRequest(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationTokenSource cts) {
            logger.Debug($"HandleUdpRequestAsync :{request}");

            //_ = Task.Run(async () => {
            //    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, request.Payload.Data));
            //    await Task.Delay(0);
            //    logger.Debug($"Response :{transferResponse}");
            //    await dataTunnelClient.SendUdpResponse(transferResponse, cancellationToken);
            //}, cancellationToken);


            var client = clientsService.ObtainUdpClient(request.Payload.Port, request.Payload.OriginPort, cts);
            await client.SendRequest(request.Payload.Data, cts.Token);
            if(!client.Listening) {
                client.Listen(request, dataTunnelClient, cts.Token);
            }
        }

        public Task HandleUdpCommand(TransferUdpCommandModel command, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleUdpCommand :{command}");

            switch(command.Payload.Command) {
                case SocketCommand.Disconnect:
                    clientsService.RemoveUdpClient(command.Payload.Port, command.Payload.OriginPort);
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }

        public async Task CreateStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream, ISignalRClient dataTunnelClient, CancellationTokenSource cts) {
            logger.Debug($"CreateStream :{streamParam}");

            var client = clientsService.ObtainTcpClient(streamParam.Port, streamParam.OriginPort, cts);
            await client.CreateStream(streamParam, stream, dataTunnelClient);
        }

        public async Task AcceptIncomingDataStream(IAsyncEnumerable<TcpStreamDataModel> stream, CancellationToken cancellationToken) {
            try {
                await foreach(var data in stream) {
                    logger.Information($"tcp client response {data}");
                }

            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }
        }

        public async IAsyncEnumerable<TcpStreamDataModel> GetOutcomingDataStream([EnumeratorCancellation] CancellationToken cancellationToken) {

            while(!cancellationToken.IsCancellationRequested) {
                await Task.Delay(200);

                var data = new TcpStreamDataModel(1, 1, Enumerable.Range(0, 200).Select(x => (byte)x).ToArray());
                logger.Information($"tcp client request {data}");

                yield return data;
            }
        }
    }
}
