using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendRequest(DataRequestModel request, Action<DataResponseModel> responseCallback);
        void ReceiveResponse(TransferResponseModel response);
    }

    public class DataTransferService : IDataTransferService {
        #region inner classes
        public class NamedRequest {
            public TransferRequestModel Parent { get; private set; }
            public Action<DataResponseModel> ResponseCallback { get; private set; }
            public NamedRequest(TransferRequestModel parent, Action<DataResponseModel> responseCallback) {
                Parent = parent;
                ResponseCallback = responseCallback;
            }
        }
        #endregion

        readonly ILogger logger;
        readonly IDateTimeService dateTimeService;
        readonly IIdService idService;
        readonly IHubContext<DataTunnelHub> hubContext;
        protected readonly ConcurrentDictionary<string, NamedRequest> requests = new();

        public DataTransferService(
                ILogger logger,
                IIdService idService,
                IDateTimeService dateTimeService,
                IHubContext<DataTunnelHub> hubContext
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(idService, nameof(idService));
            Guard.NotNull(dateTimeService, nameof(dateTimeService));
            Guard.NotNull(hubContext, nameof(hubContext));
            this.logger = logger;
            this.idService = idService;
            this.dateTimeService = dateTimeService;
            this.hubContext = hubContext;
        }

        public async Task SendRequest(DataRequestModel request, Action<DataResponseModel> responseCallback) {
            RemoveExpiredRequests();
            var namedRequest = new NamedRequest(new TransferRequestModel(request, idService.TransferRequest, dateTimeService.Now), responseCallback);

            requests.TryAdd(namedRequest.Parent.Id, namedRequest);
            logger.Information($"Request :{namedRequest.Parent}");
            await hubContext.Clients.All.SendAsync("DataRequest", namedRequest.Parent);
        }

        public void ReceiveResponse(TransferResponseModel response) {
            if(requests.TryRemove(response.Id, out NamedRequest? request)) {
                request.ResponseCallback(response.Payload);
            }
        }

        void RemoveExpiredRequests() {
            var expired = requests.Values
                .Where(x => x.Parent.Created.CompareTo(dateTimeService.Now.AddSeconds(-60)) < 0)
                .Select(x => x.Parent.Id);
            foreach(var id in expired) {
                requests.TryRemove(id, out NamedRequest? request);
            }
        }
    }
}
