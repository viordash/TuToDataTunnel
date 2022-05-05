using Microsoft.AspNetCore.SignalR;
using TutoProxy.Core.Models;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Hubs {
    public class DataTunnelHub : Hub {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;

        public DataTunnelHub(
                ILogger logger,
                IDataTransferService dataTransferService,
                IRequestProcessingService requestProcessingService) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(requestProcessingService, nameof(requestProcessingService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
        }

        public void Response(TransferResponseModel model) {
            logger.Information($"Response: {model}");
            dataTransferService.ReceiveResponse(model);
        }
    }
}
