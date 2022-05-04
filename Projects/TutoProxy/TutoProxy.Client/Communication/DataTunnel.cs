using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using TutoProxy.Client.Services;
using TutoProxy.Core.Models;

namespace TutoProxy.Client.Communication {
    public interface IDataTunnel {
        Task StartAsync(string server, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    internal class DataTunnel : IDataTunnel {
        readonly ILogger logger;
        readonly IDataReceiveService dataReceiveService;
        HubConnection? connection = null;

        public DataTunnel(
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
                 .WithUrl(server)
                 .Build();

            connection.On<string, string>("ReceiveMessage", (user, message) => {
                logger.Information($"{user}: {message}");
            });

            connection.On<TransferRequestModel>("DataRequest", async (request) => {
                var response = dataReceiveService.HandleRequest(request);
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
