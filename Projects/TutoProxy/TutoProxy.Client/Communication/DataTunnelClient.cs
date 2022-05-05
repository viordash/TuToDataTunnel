using Microsoft.AspNetCore.SignalR.Client;
using TutoProxy.Client.Services;
using TutoProxy.Core.Models;
using TuToProxy.Core;

namespace TutoProxy.Client.Communication {
    public interface IDataTunnelClient {
        Task StartAsync(string server, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    internal class DataTunnelClient : IDataTunnelClient {
        readonly ILogger logger;
        readonly IDataReceiveService dataReceiveService;
        HubConnection? connection = null;

        public DataTunnelClient(
                ILogger logger,
                IDataReceiveService dataReceiveService
                ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataReceiveService, nameof(dataReceiveService));
            this.logger = logger;
            this.dataReceiveService = dataReceiveService;
        }

        public async Task StartAsync(string server, CancellationToken cancellationToken) {
            Guard.NotNullOrEmpty(server, nameof(server));
            await StopAsync(cancellationToken);
            connection = new HubConnectionBuilder()
                 .WithUrl(new Uri(new Uri(server), DataTunnelParams.Path))
                 .Build();

            connection.On<TransferRequestModel>("DataRequest", async (request) => {
                var response = await dataReceiveService.HandleRequest(request);
                await connection.InvokeAsync("Response", response);
            });

            await connection.StartAsync(cancellationToken);
            logger.Information("Connection started");
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            if(connection != null) {
                await connection.StopAsync(cancellationToken);
                logger.Information("Connection stopped");
            }
        }
    }
}
