using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Core.Models;
using TutoProxy.Server.Hubs;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendRequest(DataRequestModel request, Action<DataResponseModel> responseCallback);
        void ReceiveResponse(TransferResponseModel response);
    }

    public class DataTransferService : IDataTransferService {
        #region inner classes
        class NamedRequest {
            public TransferRequestModel Parent { get; private set; }
            public Action<DataResponseModel> ResponseCallback { get; private set; }
            public NamedRequest(TransferRequestModel parent, Action<DataResponseModel> responseCallback) {
                Parent = parent;
                ResponseCallback = responseCallback;
            }
        }
        #endregion

        readonly ILogger logger;
        readonly IHubContext<DataTunnelHub> hubContext;
        readonly ConcurrentDictionary<string, NamedRequest> requests = new();

        public DataTransferService(
                ILogger logger,
                IHubContext<DataTunnelHub> hubContext
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(hubContext, nameof(hubContext));
            this.logger = logger;
            this.hubContext = hubContext;
        }

        public async Task SendRequest(DataRequestModel request, Action<DataResponseModel> responseCallback) {
            var namedRequest = new NamedRequest(new TransferRequestModel() {
                Id = Guid.NewGuid().ToString(),
                DateTime = DateTime.Now,
                Payload = request
            }, responseCallback);

            requests.TryAdd(namedRequest.Parent.Id, namedRequest);
            logger.Information($"Request :{namedRequest.Parent}");
            await hubContext.Clients.All.SendAsync("DataRequest", namedRequest.Parent);            
        }

        public void ReceiveResponse(TransferResponseModel response) {
            if(requests.TryRemove(response.Id, out NamedRequest? request)) {
                request.ResponseCallback(response.Payload);
            }
        }
    }
}
