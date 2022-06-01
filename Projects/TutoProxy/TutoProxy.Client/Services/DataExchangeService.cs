using System.Net;
using TutoProxy.Client.Communication;

namespace TutoProxy.Client.Services {
    public interface IDataExchangeService {
        Task HandleUdpRequestAsync(TransferUdpRequestModel request, IDataTunnelClient dataTunnelClient, CancellationToken cancellationToken);
    }

    internal class DataExchangeService : IDataExchangeService {
        readonly ILogger logger;

        public DataExchangeService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public Task HandleUdpRequestAsync(TransferUdpRequestModel request, IDataTunnelClient dataTunnelClient, CancellationToken cancellationToken) {
            logger.Debug($"HandleUdpRequestAsync :{request}");

            return Task.Run(async () => {
                var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, request.Payload.Port);
                using(var client = new UdpNetClient(remoteEndPoint, logger)) {
                    await client.SendRequest(request.Payload.Data, cancellationToken);

                    var response = await client.GetResponse(cancellationToken, TimeSpan.FromMilliseconds(5_000));
                    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(request.Payload.Port, response));
                    await dataTunnelClient.SendResponse(transferResponse, cancellationToken);
                }
            }, cancellationToken);


        }
    }
}
