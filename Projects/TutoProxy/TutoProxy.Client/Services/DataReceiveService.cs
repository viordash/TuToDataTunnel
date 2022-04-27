using TutoProxy.Core.Models;

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

            return await Task.FromResult(new TransferResponseModel() {
                Id = request.Id,
                DateTime = request.DateTime,
                Payload = new DataResponseModel() {
                    Data = $"{request.Payload?.Data}_{DateTime.Now}",
                    Protocol = "resp UDP"
                }
            });
        }
    }
}
