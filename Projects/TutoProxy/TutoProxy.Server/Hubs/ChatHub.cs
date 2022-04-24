using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Hubs {
    public class ChatHub : Hub {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;

        public ChatHub(
                ILogger logger,
                IDataTransferService dataTransferService) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
        }


        public async Task SendMessage(string user, string message) {
            logger.Information($"user: {user}, message: {message}");
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}
