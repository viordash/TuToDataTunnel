using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request, CancellationToken cancellationToken);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
        Task HandleUdpResponse(string connectionId, UdpDataResponseModel response);
        Task HandleDisconnectUdpAsync(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered);

        Task<SocketError> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
        Task<int> SendTcpRequest(TcpDataRequestModel request, CancellationToken cancellationToken);
        ValueTask<int> HandleTcpResponse(string connectionId, TcpDataResponseModel response);
        Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
        ValueTask<bool> HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress);
    }

    public class DataTransferService : IDataTransferService {
        readonly ILogger logger;
        readonly IHubContext<SignalRHub, IClientHub> typedHub;
        readonly IHubContext<SignalRHub> rawHub;
        readonly IHubClientsService clientsService;

        public DataTransferService(
                ILogger logger,
                IHubContext<SignalRHub, IClientHub> typedHubContext,
                IHubContext<SignalRHub> rawHubContext,
                IHubClientsService clientsService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(typedHubContext, nameof(typedHubContext));
            Guard.NotNull(rawHubContext, nameof(rawHubContext));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.typedHub = typedHubContext;
            this.rawHub = rawHubContext;
            this.clientsService = clientsService;
        }

        public async Task SendUdpRequest(UdpDataRequestModel request, CancellationToken cancellationToken) {
            logger.Debug($"UdpRequest :{request}");
            var connectionId = clientsService.GetConnectionIdForUdp(request.Port);
            await typedHub.Clients.Client(connectionId).UdpRequest(request);
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug($"DisconnectUdp :{socketAddress}, {totalTransfered}");
            var connectionId = clientsService.GetConnectionIdForUdp(socketAddress.Port);
            await typedHub.Clients.Client(connectionId).DisconnectUdp(socketAddress, totalTransfered);
        }

        public async Task HandleUdpResponse(string connectionId, UdpDataResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendUdpResponse(response);
        }

        public async Task HandleDisconnectUdpAsync(string connectionId, SocketAddressModel socketAddress, Int64 totalTransfered) {
            var client = clientsService.GetClient(connectionId);
            await client.DisconnectUdpAsync(socketAddress, totalTransfered);
        }

        public Task<SocketError> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug($"ConnectTcp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            return rawHub.Clients.Client(connectionId).InvokeAsync<SocketError>(ClientMethods.ConnectTcp, socketAddress, cancellationToken);
        }

        public async Task<int> SendTcpRequest(TcpDataRequestModel request, CancellationToken cancellationToken) {
            logger.Debug($"TcpRequest :{request}");
            var connectionId = clientsService.GetConnectionIdForTcp(request.Port);
            return await rawHub.Clients.Client(connectionId).InvokeAsync<int>(ClientMethods.TcpRequest, request, cancellationToken);
        }

        public ValueTask<int> HandleTcpResponse(string connectionId, TcpDataResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            return client.SendTcpResponse(response);
        }

        public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug($"DisconnectTcp :{socketAddress}");
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            return rawHub.Clients.Client(connectionId).InvokeAsync<bool>(ClientMethods.DisconnectTcp, socketAddress, cancellationToken);
        }

        public ValueTask<bool> HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress) {
            var client = clientsService.GetClient(connectionId);
            return client.DisconnectTcp(socketAddress);
        }
    }
}
