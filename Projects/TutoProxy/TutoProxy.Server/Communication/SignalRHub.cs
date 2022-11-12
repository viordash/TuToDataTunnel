using System.Diagnostics;
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

        public async Task UdpResponse(TransferUdpResponseModel model) {
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

        public async Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            logger.Debug($"DisconnectTcp: {socketAddress}, {totalTransfered}");
            Debug.WriteLine($"server HandleDisconnectTcp :{socketAddress}, {totalTransfered}");
            try {
                dataTransferService.HandleDisconnectTcp(Context.ConnectionId, socketAddress, totalTransfered);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
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

        public IAsyncEnumerable<TcpStreamDataModel> StreamToTcpClient() {
            var client = clientsService.GetClient(Context.ConnectionId);
            return client.StreamToTcpClient();
        }

        public Task StreamFromTcpClient(IAsyncEnumerable<TcpStreamDataModel> stream) {
            var client = clientsService.GetClient(Context.ConnectionId);
            return client.StreamFromTcpClient(stream);
        }
    }
}
