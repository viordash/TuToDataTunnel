using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
        Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response);
        void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered);

        Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered);
        void HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered);
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

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug($"DisconnectUdp :{socketAddress}, {totalTransfered}");
            var connectionId = clientsService.GetConnectionIdForUdp(socketAddress.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("DisconnectUdp", socketAddress, totalTransfered);
        }

        public async Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendUdpResponse(response.Payload);
        }

        public void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered) {
            var client = clientsService.GetClient(connectionId);
            client.DisconnectUdp(socketAddress, totalTransfered);
        }

        public async Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug($"DisconnectTcp :{socketAddress}, {totalTransfered}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("DisconnectTcp", socketAddress, totalTransfered);
        }

        public void HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered) {
            var client = clientsService.GetClient(connectionId);
            client.DisconnectTcp(socketAddress, totalTransfered);
        }
    }
}
