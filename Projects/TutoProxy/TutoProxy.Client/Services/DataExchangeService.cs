using TutoProxy.Client.Communication;
using TuToProxy.Core;

namespace TutoProxy.Client.Services {
    public interface IDataExchangeService {
        Task HandleUdpRequestAsync(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken);
        Task HandleTcpRequestAsync(TransferTcpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken);
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

        public Task HandleTcpRequestAsync(TransferTcpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleTcpRequestAsync :{request}");

            return Task.Run(async () => {
                var transferResponse = new TransferTcpResponseModel(request, new TcpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, request.Payload.Data));
                await Task.Delay(0);
                logger.Information($"Response :{transferResponse}");
                await dataTunnelClient.SendTcpResponse(transferResponse, cancellationToken);
            }, cancellationToken);


            //return Task.Run(async () => {
            //    var client = clientsService.GetTcpClient(request.Payload.Port);
            //    await client.SendRequest(request.Payload.Data, cancellationToken);

            //    var response = await client.GetResponse(cancellationToken, TcpSocketParams.ReceiveTimeout);
            //    var transferResponse = new TransferTcpResponseModel(request, new TcpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, response));
            //    await dataTunnelClient.SendTcpResponse(transferResponse, cancellationToken);
            //}, cancellationToken);
        }

        public Task HandleUdpRequestAsync(TransferUdpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleUdpRequestAsync :{request}");

            //return Task.Run(async () => {
            //    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.Data));
            //    await Task.Delay(0);
            //    logger.Information($"Response :{transferResponse}");
            //    await dataTunnelClient.SendResponse(transferResponse, cancellationToken);
            //}, cancellationToken);

            return Task.Run(async () => {
                var client = clientsService.GetUdpClient(request.Payload.Port);
                await client.SendRequest(request.Payload.Data, cancellationToken);

                var response = await client.GetResponse(cancellationToken, UdpSocketParams.ReceiveTimeout);
                var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, request.Payload.RemotePort, response));
                await dataTunnelClient.SendUdpResponse(transferResponse, cancellationToken);
            }, cancellationToken);


        }
    }
}
