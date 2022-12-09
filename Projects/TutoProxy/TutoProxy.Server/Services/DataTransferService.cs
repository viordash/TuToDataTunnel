using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
        Task HandleUdpResponse(string connectionId, UdpDataResponseModel response);
        void HandleDisconnectUdp(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered);

        Task<SocketError> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
        Task<int> SendTcpRequest(TcpDataRequestModel request, CancellationToken cancellationToken);
        ValueTask<int> HandleTcpResponse(string connectionId, TcpDataResponseModel response);
        Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
        ValueTask<bool> HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress);
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

        public Task<SocketError> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug($"ConnectTcp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            return signalHub.Clients.Client(connectionId).InvokeAsync<SocketError>("ConnectTcp", socketAddress, cancellationToken);
        }

        public async Task<int> SendTcpRequest(TcpDataRequestModel request, CancellationToken cancellationToken) {
            logger.Debug($"TcpRequest :{request}");
            var connectionId = clientsService.GetConnectionIdForTcp(request.Port);
            return await signalHub.Clients.Client(connectionId).InvokeAsync<int>("TcpRequest", request, cancellationToken);
        }

        public ValueTask<int> HandleTcpResponse(string connectionId, TcpDataResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            return client.SendTcpResponse(response);
        }

        public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug($"DisconnectTcp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            return signalHub.Clients.Client(connectionId).InvokeAsync<bool>("DisconnectTcp", socketAddress, cancellationToken);
        }

        public ValueTask<bool> HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress) {
            var client = clientsService.GetClient(connectionId);
            return client.DisconnectTcp(socketAddress);
        }
    }
}
