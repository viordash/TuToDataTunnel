using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendTcpRequest(TcpDataRequestModel request);
        Task HandleTcpResponse(string connectionId, TransferTcpResponseModel response);
        Task SendUdpRequest(UdpDataRequestModel request);
        Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response);
    }

    public class DataTransferService : IDataTransferService {
        readonly ILogger logger;
        readonly IDateTimeService dateTimeService;
        readonly IIdService idService;
        readonly IHubContext<SignalRHub> signalHub;
        readonly IHubClientsService clientsService;

        public DataTransferService(
                ILogger logger,
                IIdService idService,
                IDateTimeService dateTimeService,
                IHubContext<SignalRHub> hubContext,
                IHubClientsService clientsService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(idService, nameof(idService));
            Guard.NotNull(dateTimeService, nameof(dateTimeService));
            Guard.NotNull(hubContext, nameof(hubContext));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.idService = idService;
            this.dateTimeService = dateTimeService;
            this.signalHub = hubContext;
            this.clientsService = clientsService;
        }

        public async Task SendTcpRequest(TcpDataRequestModel request) {
            var transferRequest = new TransferTcpRequestModel(request, idService.TransferRequest, dateTimeService.Now);
            logger.Debug($"TcpRequest :{transferRequest}");
            await signalHub.Clients.All.SendAsync("TcpRequest", transferRequest);
        }

        public async Task HandleTcpResponse(string connectionId, TransferTcpResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendTcpResponse(response.Payload);
        }

        public async Task SendUdpRequest(UdpDataRequestModel request) {
            var transferRequest = new TransferUdpRequestModel(request, idService.TransferRequest, dateTimeService.Now);
            logger.Debug($"UdpRequest :{transferRequest}");
            await signalHub.Clients.All.SendAsync("UdpRequest", transferRequest);
        }

        public async Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendUdpResponse(response.Payload);
        }
    }
}
