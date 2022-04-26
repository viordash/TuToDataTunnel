using Microsoft.AspNetCore.SignalR;
using TutoProxy.Core.Models;
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

        public async Task SendMessage(string connectionId, string user, string message) {
            logger.Information($"user: {user}, message: {message}");
            await Clients.All.SendAsync("ReceiveMessage", user, message);

            _ = Task.Run(async () => {
                await Task.Delay(1000);
                var response = await dataTransferService.SendRequest(DateTime.Now.ToLongDateString());
                logger.Information($"received response: {response}");
            });
        }

        public async Task Response(DataTransferResponseModel model) {
            logger.Information($"Response: {model}");
            await dataTransferService.ReceiveResponse(model);
        }
    }
}
