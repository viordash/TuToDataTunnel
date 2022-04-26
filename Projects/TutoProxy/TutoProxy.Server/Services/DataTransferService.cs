using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Core.Models;
using TutoProxy.Server.Hubs;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task<DataTransferResponseModel> SendRequest(string payload);
        Task ReceiveResponse(DataTransferResponseModel response);
    }

    public class DataTransferService : IDataTransferService {
        readonly ILogger logger;
        readonly IHubContext<ChatHub> hubContext;
        readonly ConcurrentDictionary<string, DataTransferRequestModel> requests = new();

        public DataTransferService(
                ILogger logger,
                IHubContext<ChatHub> hubContext
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(hubContext, nameof(hubContext));
            this.logger = logger;
            this.hubContext = hubContext;
        }

        public async Task<DataTransferResponseModel> SendRequest(string payload) {
            logger.Information($"Request :{payload}");

            var request = new DataTransferRequestModel() {
                Id = Guid.NewGuid().ToString(),
                Payload = DateTime.Now.ToString()
            };
            requests.TryAdd(request.Id, request);

            await hubContext.Clients.All.SendAsync("DataRequest", request);

            await Task.Delay(1000);
            return new DataTransferResponseModel();
        }

        public async Task ReceiveResponse(DataTransferResponseModel response) {
            if(requests.TryGetValue(response.Id, out DataTransferRequestModel? request)) {
                logger.Information($"Response :{request}");
            }

            await Task.CompletedTask;
        }
    }
}
