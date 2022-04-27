using TutoProxy.Core.Models;

namespace TutoProxy.Server.Services {
    public interface IRequestProcessingService {
        Task<DataResponseModel> Request(DataRequestModel request);
    }

    public class RequestProcessingService : IRequestProcessingService {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;


        public RequestProcessingService(
                ILogger logger,
                IDataTransferService dataTransferService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
        }

        public async Task<DataResponseModel> Request(DataRequestModel request) {
            var waitResponse = new TaskCompletionSource<DataResponseModel>();

            await dataTransferService.SendRequest(request, (response) => {
                waitResponse.TrySetResult(response);
            });

            var sss = await waitResponse.Task;
            return sss;
        }

    }
}
