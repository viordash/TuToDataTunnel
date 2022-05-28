using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request, Action<UdpDataResponseModel> responseCallback);
        void ReceiveUdpResponse(TransferUdpResponseModel response);
    }

    public class DataTransferService : IDataTransferService {
        #region inner classes
        public class NamedUdpRequest {
            public TransferUdpRequestModel Parent { get; private set; }
            public Action<UdpDataResponseModel> ResponseCallback { get; private set; }
            public NamedUdpRequest(TransferUdpRequestModel parent, Action<UdpDataResponseModel> responseCallback) {
                Parent = parent;
                ResponseCallback = responseCallback;
            }
        }
        #endregion

        readonly ILogger logger;
        readonly IDateTimeService dateTimeService;
        readonly IIdService idService;
        readonly IHubContext<DataTunnelHub> hubContext;
        protected readonly ConcurrentDictionary<string, NamedUdpRequest> udpRequests = new();

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

        public async Task SendUdpRequest(UdpDataRequestModel request, Action<UdpDataResponseModel> responseCallback) {
            RemoveExpiredRequests();
            var namedRequest = new NamedUdpRequest(new TransferUdpRequestModel(request, idService.TransferRequest, dateTimeService.Now), responseCallback);

            udpRequests.TryAdd(namedRequest.Parent.Id, namedRequest);
            logger.Information($"UdpRequest :{namedRequest.Parent}");
            await hubContext.Clients.All.SendAsync("UdpRequest", namedRequest.Parent);
        }

        public void ReceiveUdpResponse(TransferUdpResponseModel response) {
            if(udpRequests.TryRemove(response.Id, out NamedUdpRequest? request)) {
                request.ResponseCallback(response.Payload);
            }
        }

        void RemoveExpiredRequests() {
            var expired = udpRequests.Values
                .Where(x => x.Parent.Created.CompareTo(dateTimeService.Now.AddSeconds(-60)) < 0)
                .Select(x => x.Parent.Id);
            foreach(var id in expired) {
                udpRequests.TryRemove(id, out NamedUdpRequest? request);
            }
        }
    }
}
