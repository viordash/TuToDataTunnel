using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Hubs;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task SendUdpRequest(UdpDataRequestModel request);
        Task SendUdpCommand(UdpCommandModel command);
        Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response);
        Task HandleUdpCommand(string connectionId, TransferUdpCommandModel command);

        Task CreateTcpStream(TcpStreamParam streamParam, CancellationToken cancellationToken);
        IAsyncEnumerable<byte[]> TcpStream2Cln(string connectionId, TcpStreamParam streamParam);
        Task TcpStream2Srv(string connectionId, TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream);
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

        public async Task SendUdpRequest(UdpDataRequestModel request) {
            var transferRequest = new TransferUdpRequestModel(request, idService.TransferRequest, dateTimeService.Now);
            logger.Debug($"UdpRequest :{transferRequest}");
            var connectionId = clientsService.GetConnectionIdForUdp(request.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("UdpRequest", transferRequest);
        }

        public async Task SendUdpCommand(UdpCommandModel command) {
            var transferCommand = new TransferUdpCommandModel(idService.TransferRequest, dateTimeService.Now, command);
            logger.Debug($"UdpCommand :{transferCommand}");
            var connectionId = clientsService.GetConnectionIdForUdp(command.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("UdpCommand", transferCommand);
        }

        public async Task HandleUdpResponse(string connectionId, TransferUdpResponseModel response) {
            var client = clientsService.GetClient(connectionId);
            await client.SendUdpResponse(response.Payload);
        }

        public async Task HandleUdpCommand(string connectionId, TransferUdpCommandModel command) {
            var client = clientsService.GetClient(connectionId);
            await client.ProcessUdpCommand(command.Payload);
        }


        public async Task CreateTcpStream(TcpStreamParam streamParam, CancellationToken cancellationToken) {
            logger.Debug($"CreateStream :{streamParam}");
            var connectionId = clientsService.GetConnectionIdForTcp(streamParam.Port);
            await signalHub.Clients.Client(connectionId).SendAsync("CreateStream", streamParam, cancellationToken);
        }

        public IAsyncEnumerable<byte[]> TcpStream2Cln(string connectionId, TcpStreamParam streamParam) {
            var client = clientsService.GetClient(connectionId);
            return client.TcpStream2Cln(streamParam);
        }

        public async Task TcpStream2Srv(string connectionId, TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream) {
            var client = clientsService.GetClient(connectionId);
            await client.TcpStream2Srv(streamParam, stream);
        }
    }
}
