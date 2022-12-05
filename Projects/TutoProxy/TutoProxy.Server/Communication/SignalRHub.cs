using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Hubs {
    public class SignalRHub : Hub {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;
        readonly IHubClientsService clientsService;

        public SignalRHub(
                ILogger logger,
                IDataTransferService dataTransferService,
                IHubClientsService clientsService) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
            this.clientsService = clientsService;
        }

        public async Task UdpResponse(UdpDataResponseModel model) {
            logger.Debug($"UdpResponse: {model}");
            try {
                await dataTransferService.HandleUdpResponse(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug($"DisconnectUdp: {socketAddress}, {totalTransfered}");
            try {
                dataTransferService.HandleDisconnectUdp(Context.ConnectionId, socketAddress, totalTransfered);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public async Task<int> TcpResponse(TcpDataResponseModel model) {
            logger.Debug($"TcpResponse: {model}");
            try {
                return await dataTransferService.HandleTcpResponse(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
                return -1;
            }
        }

        public async Task<bool> DisconnectTcp(SocketAddressModel socketAddress) {
            logger.Debug($"DisconnectTcp: {socketAddress}");
            try {
                return await dataTransferService.HandleDisconnectTcp(Context.ConnectionId, socketAddress);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
                return false;
            }
        }

        public override async Task OnConnectedAsync() {
            try {
                var queryString = Context.GetHttpContext()?.Request.QueryString.Value;
                clientsService.Connect(Context.ConnectionId, Clients.Caller, queryString);
            } catch(TuToException ex) {
                logger.Error(ex.Message);
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception) {
            clientsService.Disconnect(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
