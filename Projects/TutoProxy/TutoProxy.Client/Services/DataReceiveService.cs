using TutoProxy.Core.Models;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IDataReceiveService {
        Task<TransferResponseModel> HandleRequest(TransferRequestModel request);
    }

    internal class DataReceiveService : IDataReceiveService {
        readonly ILogger logger;
        public DataReceiveService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public async Task<TransferResponseModel> HandleRequest(TransferRequestModel request) {
            logger.Information($"HandleRequest :{request}");

            var response = new TransferResponseModel(request,
                DataResponseFactory.Create(request.Payload.Protocol, request.Payload.Data)
                );
            await Task.Delay(300);
            logger.Information($"Response :{response}");
            return response;
        }
    }
}
