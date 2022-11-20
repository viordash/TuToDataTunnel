using TutoProxy.Client.Communication;

namespace TutoProxy.Client.Services {
    public interface IDataExchangeService {
        Task HandleUdpRequest(UdpDataRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken);
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

        public async Task HandleUdpRequest(UdpDataRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleUdpRequestAsync :{request}");

            //_ = Task.Run(async () => {
            //    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, request.Payload.Data));
            //    await Task.Delay(0);
            //    logger.Debug($"Response :{transferResponse}");
            //    await dataTunnelClient.SendUdpResponse(transferResponse, cancellationToken);
            //}, cancellationToken);


            var client = clientsService.ObtainUdpClient(request.Port, request.OriginPort, dataTunnelClient);
            await client.SendRequest(request.Data!, cancellationToken);
            if(!client.Listening) {
                client.Listen(request, dataTunnelClient, cancellationToken);
            }
        }

    }
}
