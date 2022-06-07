using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendTcpRequest(TcpDataRequestModel request);
        Task HandleTcpResponse(TransferTcpResponseModel response);
        Task SendUdpRequest(UdpDataRequestModel request);
        Task HandleUdpResponse(TransferUdpResponseModel response);
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
            logger.Information($"TcpRequest :{transferRequest}");
            await signalHub.Clients.All.SendAsync("TcpRequest", transferRequest);
        }

        public async Task HandleTcpResponse(TransferTcpResponseModel response) {
            var client = clientsService.GetTcpClient(response.Payload.Port);
            if(client == null) {
                throw new ClientNotFoundException(DataProtocol.Udp, response.Payload.Port);
            }
            await client.SendTcpResponse(response.Payload);
        }

        public async Task SendUdpRequest(UdpDataRequestModel request) {
            var transferRequest = new TransferUdpRequestModel(request, idService.TransferRequest, dateTimeService.Now);
            logger.Information($"UdpRequest :{transferRequest}");
            await signalHub.Clients.All.SendAsync("UdpRequest", transferRequest);
        }

        public async Task HandleUdpResponse(TransferUdpResponseModel response) {
            var client = clientsService.GetUdpClient(response.Payload.Port);
            if(client == null) {
                throw new ClientNotFoundException(DataProtocol.Udp, response.Payload.Port);
            }
            await client.SendUdpResponse(response.Payload);
        }
    }
}
