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
            logger.Debug("UdpRequest :{request}", request);
            var connectionId = clientsService.GetConnectionIdForUdp(request.Port);
            await typedHub.Clients.Client(connectionId).UdpRequest(request);
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug("DisconnectUdp :{socketAddress}, {totalTransfered}", socketAddress, totalTransfered);
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

        public async Task<SocketError> ConnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug("ConnectTcp :{SocketAddress}", socketAddress);
            // Get connectionId using round-robin and register this session
            var connectionId = clientsService.GetConnectionIdForTcp(socketAddress.Port);
            clientsService.RegisterTcpSession(socketAddress.Port, socketAddress.OriginPort, connectionId);
            try {
                return await rawHub.Clients.Client(connectionId).InvokeAsync<SocketError>(ClientMethods.ConnectTcp, socketAddress, cancellationToken);
            } catch {
                clientsService.UnregisterTcpSession(socketAddress.Port, socketAddress.OriginPort);
                throw;
            }
        }

        public async Task<int> SendTcpRequest(TcpDataRequestModel request, CancellationToken cancellationToken) {
            logger.Debug("TcpRequest :{Request}", request);
            // Use registered session connectionId for session affinity
            var connectionId = clientsService.GetConnectionIdForTcpSession(request.Port, request.OriginPort);
            return await rawHub.Clients.Client(connectionId).InvokeAsync<int>(ClientMethods.TcpRequest, request, cancellationToken);
        }

        public ValueTask<int> HandleTcpResponse(string connectionId, TcpDataResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            return client.SendTcpResponse(response);
        }

        public async Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            logger.Debug("DisconnectTcp :{SocketAddress}", socketAddress);
            // Use registered session connectionId
            var connectionId = clientsService.GetConnectionIdForTcpSession(socketAddress.Port, socketAddress.OriginPort);
            try {
                return await rawHub.Clients.Client(connectionId).InvokeAsync<bool>(ClientMethods.DisconnectTcp, socketAddress, cancellationToken);
            } finally {
                clientsService.UnregisterTcpSession(socketAddress.Port, socketAddress.OriginPort);
            }
        }

        public ValueTask<bool> HandleDisconnectTcp(string connectionId, SocketAddressModel socketAddress) {
            var client = clientsService.GetClient(connectionId);
            // Also unregister the session when client initiates disconnect
            clientsService.UnregisterTcpSession(socketAddress.Port, socketAddress.OriginPort);
            return client.DisconnectTcp(socketAddress);
        }
    }
}
