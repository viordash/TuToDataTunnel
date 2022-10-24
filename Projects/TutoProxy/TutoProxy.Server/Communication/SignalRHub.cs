using System.Threading;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;

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

        public IAsyncEnumerable<byte[]> TcpStream2Cln(TcpStreamParam streamParam, CancellationToken cancellationToken) {
            logger.Debug($"TcpStream2Cln: {streamParam}");
            return dataTransferService.TcpStream2Cln(Context.ConnectionId, streamParam, cancellationToken);
        }

        public async Task TcpStream2Srv(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream) {
            logger.Debug($"TcpStream2Srv: {streamParam}");
            await dataTransferService.TcpStream2Srv(Context.ConnectionId, streamParam, stream);
        }
    }
}
