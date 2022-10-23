using System.Runtime.CompilerServices;
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

        public async Task TcpResponse(TransferTcpResponseModel model) {
            logger.Debug($"TcpResponse: {model}");
            try {
                await dataTransferService.HandleTcpResponse(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public async Task TcpCommand(TransferTcpCommandModel model) {
            logger.Debug($"TcpCommand: {model}");
            try {
                await dataTransferService.HandleTcpCommand(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public async Task UdpResponse(TransferUdpResponseModel model) {
            logger.Debug($"UdpResponse: {model}");
            try {
                await dataTransferService.HandleUdpResponse(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public async Task UdpCommand(TransferUdpCommandModel model) {
            logger.Debug($"UdpCommand: {model}");
            try {
                await dataTransferService.HandleUdpCommand(Context.ConnectionId, model);
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

        public async IAsyncEnumerable<byte[]> TcpStream(int port, int originPort, [EnumeratorCancellation] CancellationToken cancellationToken) {
            for(var i = 0; i < port; i++) {
                // Check the cancellation token regularly so that the server will stop
                // producing items if the client disconnects.
                cancellationToken.ThrowIfCancellationRequested();

                yield return new byte[] { (byte)i, (byte)(i >> 8) };

                // Use the cancellationToken in other APIs that accept cancellation
                // tokens so the cancellation can flow down to them.
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}
