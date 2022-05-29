using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request);
        Task HandleUdpResponse(TransferUdpResponseModel response);
    }

    public class DataTransferService : IDataTransferService {
        readonly ILogger logger;
        readonly IDateTimeService dateTimeService;
        readonly IIdService idService;
        readonly IHubContext<DataTunnelHub> hubContext;
        readonly IClientsService clientsService;

        public DataTransferService(
                ILogger logger,
                IIdService idService,
                IDateTimeService dateTimeService,
                IHubContext<DataTunnelHub> hubContext,
                IClientsService clientsService
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(idService, nameof(idService));
            Guard.NotNull(dateTimeService, nameof(dateTimeService));
            Guard.NotNull(hubContext, nameof(hubContext));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.idService = idService;
            this.dateTimeService = dateTimeService;
            this.hubContext = hubContext;
            this.clientsService = clientsService;
        }

        public async Task SendUdpRequest(UdpDataRequestModel request) {
            var transferRequest = new TransferUdpRequestModel(request, idService.TransferRequest, dateTimeService.Now);
            logger.Information($"UdpRequest :{transferRequest}");
            await hubContext.Clients.All.SendAsync("UdpRequest", transferRequest);
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
