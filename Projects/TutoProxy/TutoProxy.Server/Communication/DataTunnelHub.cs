using Microsoft.AspNetCore.SignalR;
using TutoProxy.Core.Models;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Hubs {
    public class DataTunnelHub : Hub {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;
        readonly IRequestProcessingService requestProcessingService;

        public DataTunnelHub(
                ILogger logger,
                IDataTransferService dataTransferService,
                IRequestProcessingService requestProcessingService) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(requestProcessingService, nameof(requestProcessingService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
            this.requestProcessingService = requestProcessingService;
        }

        public async Task SendMessage(string connectionId, string user, string message) {
            logger.Information($"user: {user}, message: {message}");
            await Clients.All.SendAsync("ReceiveMessage", user, message);

            _ = Task.Run(async () => {
                await Task.Delay(300);
                await requestProcessingService.Request(new DataRequestModel() {
                    Data = message,
                    Protocol = "req TCP"
                });
            });
        }

        public void Response(TransferResponseModel model) {
            logger.Information($"Response: {model}");
            dataTransferService.ReceiveResponse(model);
        }
    }
}
