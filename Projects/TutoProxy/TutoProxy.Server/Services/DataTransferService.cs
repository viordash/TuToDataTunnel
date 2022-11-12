using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request);
        Task DisconnectUdp(SocketAddressModel socketAddress);
        Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response);
        void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress);


        Task DisconnectTcp(SocketAddressModel socketAddress);
        void HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress);
    }

    public class DataTransferService : IDataTransferService {
        readonly ILogger logger;
        readonly IDateTimeService dateTimeService;
        readonly IIdService idService;
        readonly IHubContext<SignalRHub> signalHub;
        readonly IHubClientsService clientsService;

        public DataTransferService(
                ILogger logger,
                IIdService idService,
                IDateTimeService dateTimeService,
                IHubContext<SignalRHub> hubContext,
                IHubClientsService clientsService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(idService, nameof(idService));
            Guard.NotNull(dateTimeService, nameof(dateTimeService));
            Guard.NotNull(hubContext, nameof(hubContext));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.idService = idService;
            this.dateTimeService = dateTimeService;
            this.signalHub = hubContext;
            this.clientsService = clientsService;
        }

        public async Task SendUdpRequest(UdpDataRequestModel request) {
            var transferRequest = new TransferUdpRequestModel(request, idService.TransferRequest, dateTimeService.Now);
            logger.Debug($"UdpRequest :{transferRequest}");
            var connectionId = clientsService.GetConnectionIdForUdp(request.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("UdpRequest", transferRequest);
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress) {
            logger.Debug($"DisconnectUdp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForUdp(socketAddress.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("DisconnectUdp", socketAddress);
        }

        public async Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendUdpResponse(response.Payload);
        }

        public void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress) {
            var client = clientsService.GetClient(connectionId);
            client.DisconnectUdp(socketAddress);
        }

        public async Task DisconnectTcp(SocketAddressModel socketAddress) {
            logger.Debug($"DisconnectTcp :{socketAddress}");
            Debug.WriteLine($"server DisconnectTcp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("DisconnectTcp", socketAddress);
        }

        public void HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress) {
            var client = clientsService.GetClient(connectionId);
            client.DisconnectTcp(socketAddress);
        }
    }
}
