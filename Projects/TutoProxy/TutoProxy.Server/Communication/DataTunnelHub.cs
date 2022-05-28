using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Hubs {
    public class DataTunnelHub : Hub {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;
        readonly IClientsService clientsService;

        public DataTunnelHub(
                ILogger logger,
                IDataTransferService dataTransferService,
                IClientsService clientsService) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
            this.clientsService = clientsService;
        }

        public void UdpResponse(TransferUdpResponseModel model) {
            logger.Information($"UdpResponse: {model}");
            dataTransferService.ReceiveUdpResponse(model);
        }

        public override async Task OnConnectedAsync() {
            var queryString = Context.GetHttpContext()?.Request.QueryString.Value;
            if(queryString != null) {
                await clientsService.ConnectAsync(Context.ConnectionId, Clients.Caller, queryString);
            } else {
                await Clients.Caller.SendAsync("Errors", "QueryString empty");
            }
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception) {
            clientsService.Disconnect(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
