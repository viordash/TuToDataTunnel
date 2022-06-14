using TutoProxy.Client.Communication;
using TuToProxy.Core;

namespace TutoProxy.Client.Services {
    public interface IDataExchangeService {
        Task HandleTcpRequest(TransferTcpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken);
        void HandleUdpRequest(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken);
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

        public async Task HandleTcpRequest(TransferTcpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleTcpRequestAsync :{request}");

            //_ = Task.Run(async () => {
            //    var transferResponse = new TransferTcpResponseModel(request, new TcpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, request.Payload.Data));
            //    await Task.Delay(0);
            //    logger.Debug($"Response :{transferResponse}");
            //    await dataTunnelClient.SendTcpResponse(transferResponse, cancellationToken);
            //}, cancellationToken);

            var client = clientsService.ObtainClient(request.Payload);
            await client.SendRequest(request.Payload.Data, cancellationToken);
            if(!client.Listening) {
                client.Listen(request, dataTunnelClient, cancellationToken);
            }
        }

        public void HandleUdpRequest(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleUdpRequestAsync :{request}");

            //_ = Task.Run(async () => {
            //    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, request.Payload.Data));
            //    await Task.Delay(0);
            //    logger.Debug($"Response :{transferResponse}");
            //    await dataTunnelClient.SendUdpResponse(transferResponse, cancellationToken);
            //}, cancellationToken);

            _ = Task.Run(async () => {
                var client = clientsService.ObtainClient(request.Payload);
                await client.SendRequest(request.Payload.Data, cancellationToken);
                var response = await client.GetResponse(cancellationToken, UdpSocketParams.ReceiveTimeout);
                var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.OriginPort, response));

                await dataTunnelClient.SendUdpResponse(transferResponse, cancellationToken);
            }, cancellationToken);


        }
    }
}
