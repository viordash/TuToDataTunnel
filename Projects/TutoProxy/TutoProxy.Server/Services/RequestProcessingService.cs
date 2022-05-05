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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var waitResponse = new TaskCompletionSource<DataResponseModel>();
            cts.Token.Register(() => waitResponse.TrySetCanceled(), useSynchronizationContext: false);
            await dataTransferService.SendRequest(request, (response) => {
                waitResponse.TrySetResult(response);
            });
            return await waitResponse.Task;
        }

    }
}
