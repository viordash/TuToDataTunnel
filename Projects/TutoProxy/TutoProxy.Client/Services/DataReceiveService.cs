using System.Net;
using TutoProxy.Client.Communication;
using TutoProxy.Core.Models;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IDataReceiveService {
        Task<TransferResponseModel> HandleRequest(TransferRequestModel request, CancellationToken cancellationToken);
    }

    internal class DataReceiveService : IDataReceiveService {
        readonly ILogger logger;

        public DataReceiveService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;

        }

        public async Task<TransferResponseModel> HandleRequest(TransferRequestModel request, CancellationToken cancellationToken) {
            logger.Information($"HandleRequest :{request}");

            switch(request.Payload.Protocol) {
                case DataProtocol.Udp: {
                    var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, request.Payload.Port);
                    using(var client = new UdpNetClient(remoteEndPoint, logger)) {
                        await client.SendRequest(request.Payload.Data, cancellationToken);

                    }
                    break;
                }
                default:
                    throw new NotImplementedException();
            }


            var response = new TransferResponseModel(request,
                DataResponseFactory.Create(request.Payload.Protocol, request.Payload.Data)
                );
            await Task.Delay(300);
            logger.Information($"Response :{response}");
            return response;
        }
    }
}
