using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
        Task HandleUdpResponse(string connectionId, UdpDataResponseModel response);
        void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered);

        Task<bool> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
        Task SendTcpRequest(TcpDataRequestModel request);
        Task HandleTcpResponse(string connectionId, TcpDataResponseModel response);
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
            logger.Debug($"UdpRequest :{request}");
            var connectionId = clientsService.GetConnectionIdForUdp(request.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("UdpRequest", request);
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug($"DisconnectUdp :{socketAddress}, {totalTransfered}");
            var connectionId = clientsService.GetConnectionIdForUdp(socketAddress.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("DisconnectUdp", socketAddress, totalTransfered);
        }

        public async Task HandleUdpResponse(string connectionId, UdpDataResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendUdpResponse(response);
        }

        public void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered) {
            var client = clientsService.GetClient(connectionId);
            client.DisconnectUdp(socketAddress, totalTransfered);
        }

        public Task<bool> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug($"ConnectTcp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            return signalHub.Clients.Client(connectionId).InvokeAsync<bool>("ConnectTcp", socketAddress, cancellationToken);
        }

        public async Task SendTcpRequest(TcpDataRequestModel request) {
            logger.Debug($"TcpRequest :{request}");
            var connectionId = clientsService.GetConnectionIdForTcp(request.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("TcpRequest", request);
        }

        public async Task HandleTcpResponse(string connectionId, TcpDataResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendTcpResponse(response);
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
