using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using TutoProxy.Server.Services;
using TuToProxy.Core;

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

        public override Task OnConnectedAsync() {
            var queryString = QueryHelpers.ParseQuery(Context.GetHttpContext()?.Request.QueryString.Value);
            var tcpQuery = queryString[DataTunnelParams.TcpQuery];
            var udpQuery = queryString[DataTunnelParams.UdpQuery];

            if(tcpQuery == "reserved 1.0" || udpQuery == "ssss") {
                //Clients.Caller.notifyWrongVersion();
            }
            return base.OnConnectedAsync();
        }
    }
}
