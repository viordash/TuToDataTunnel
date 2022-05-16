﻿using TutoProxy.Core.Models;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IRequestProcessingService {
        Task<DataResponseModel> Request(DataRequestModel request);
    }

    public class RequestProcessingService : IRequestProcessingService {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;
        readonly IDateTimeService dateTimeService;


        public RequestProcessingService(
                ILogger logger,
                IDataTransferService dataTransferService,
                IDateTimeService dateTimeService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(dateTimeService, nameof(dateTimeService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
            this.dateTimeService = dateTimeService;
        }

        public async Task<DataResponseModel> Request(DataRequestModel request) {
            using var cts = new CancellationTokenSource(dateTimeService.RequestTimeout);
            var waitResponse = new TaskCompletionSource<DataResponseModel>();
            cts.Token.Register(() => waitResponse.TrySetCanceled(), useSynchronizationContext: false);
            await dataTransferService.SendRequest(request, (response) => {
                waitResponse.TrySetResult(response);
            });
            return await waitResponse.Task;
        }

    }
}
