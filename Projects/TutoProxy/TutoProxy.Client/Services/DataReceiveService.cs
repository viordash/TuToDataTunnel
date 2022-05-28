using System.Net;
using TutoProxy.Client.Communication;

namespace TutoProxy.Client.Services {
    public interface IDataReceiveService {
        Task<TransferUdpResponseModel> HandleUdpRequest(TransferUdpRequestModel request, CancellationToken cancellationToken);
    }

    internal class DataReceiveService : IDataReceiveService {
        readonly ILogger logger;

        public DataReceiveService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;

        }

        public async Task<TransferUdpResponseModel> HandleUdpRequest(TransferUdpRequestModel request, CancellationToken cancellationToken) {
            logger.Information($"HandleUdpRequest :{request}");

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, request.Payload.Port);
            using(var client = new UdpNetClient(remoteEndPoint, logger)) {
                await client.SendRequest(request.Payload.Data, cancellationToken);

                if(!request.Payload.FireNForget) {
                    var response = await client.GetResponse(cancellationToken, TimeSpan.FromMilliseconds(10_000));
                    var transferResponse = new TransferUdpResponseModel(request, new UdpDataResponseModel(response));
                    return transferResponse;
                } else {
                    return new TransferUdpResponseModel();
                }
            }
        }
    }
}
