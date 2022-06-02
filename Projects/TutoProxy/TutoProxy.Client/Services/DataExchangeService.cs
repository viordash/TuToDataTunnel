using System.Net;
using TutoProxy.Client.Communication;

namespace TutoProxy.Client.Services {
    public interface IDataExchangeService {
        Task HandleUdpRequestAsync(TransferUdpRequestModel request, IDataTunnelClient dataTunnelClient, CancellationToken cancellationToken);
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

        public Task HandleUdpRequestAsync(TransferUdpRequestModel request, IDataTunnelClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleUdpRequestAsync :{request}");

            //return Task.Run(async () => {
            //    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.Data));
            //    await Task.Delay(0);
            //    logger.Information($"Response :{transferResponse}");
            //    await dataTunnelClient.SendResponse(transferResponse, cancellationToken);
            //}, cancellationToken);

            return Task.Run(async () => {
                var connection = clientsService.GetUdpConnection(request.Payload.Port);
                await connection.SendRequest(request.Payload.Data, cancellationToken);

                var response = await connection.GetResponse(cancellationToken, TimeSpan.FromMilliseconds(5_000));
                var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, response));
                await dataTunnelClient.SendResponse(transferResponse, cancellationToken);
            }, cancellationToken);


        }
    }
}
