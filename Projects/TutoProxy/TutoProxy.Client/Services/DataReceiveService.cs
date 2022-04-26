using TutoProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IDataReceiveService {
        Task<DataTransferResponseModel> HandleRequest(DataTransferRequestModel request);
    }

    internal class DataReceiveService : IDataReceiveService {
        readonly ILogger logger;
        public DataReceiveService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public async Task<DataTransferResponseModel> HandleRequest(DataTransferRequestModel request) {
            logger.Information($"HandleRequest :{request}");
            await Task.Delay(1000);

            return new DataTransferResponseModel() {
                Id = request.Id,
                DateTime = request.DateTime,
                Payload = DateTime.Now.ToShortTimeString()
            };
        }
    }
}
