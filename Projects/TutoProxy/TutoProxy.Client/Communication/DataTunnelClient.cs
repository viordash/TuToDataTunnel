using System.Collections.Specialized;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using TutoProxy.Client.Services;
using TutoProxy.Core.CommandLine;
using TuToProxy.Core;

namespace TutoProxy.Client.Communication {
    public interface IDataTunnelClient {
        Task StartAsync(string server, string tcpQuery, string udpQuery, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    internal class DataTunnelClient : IDataTunnelClient {
        #region inner classes
        class RetryPolicy : IRetryPolicy {
            readonly ILogger logger;
            public RetryPolicy(ILogger logger) {
                this.logger = logger;
            }
            public TimeSpan? NextRetryDelay(RetryContext retryContext) {
                logger.Warning(retryContext.RetryReason?.Message);
                return TimeSpan.FromSeconds(Math.Min(retryContext.PreviousRetryCount + 1, 60));
            }
        }
        #endregion

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

        public async Task StartAsync(string server, string tcpQuery, string udpQuery, CancellationToken cancellationToken) {
            Guard.NotNullOrEmpty(server, nameof(server));

            await StopAsync(cancellationToken);

            var ub = new UriBuilder(server);
            ub.Path = DataTunnelParams.Path;

            var query = QueryString.Create(new[] {
                KeyValuePair.Create(DataTunnelParams.TcpQuery, tcpQuery),
                KeyValuePair.Create(DataTunnelParams.UdpQuery, udpQuery)
            });
            ub.Query = query.ToString();

            connection = new HubConnectionBuilder()
                 .WithUrl(ub.Uri)
                 .WithAutomaticReconnect(new RetryPolicy(logger))
                 .Build();

            connection.On<TransferRequestModel>("DataRequest", async (request) => {
                var response = await dataReceiveService.HandleRequest(request);
                await connection.InvokeAsync("Response", response);
            });

            connection.Reconnecting += e => {
                logger.Warning($"Connection lost. Reconnecting");
                return Task.CompletedTask;
            };

            connection.Reconnected += s => {
                logger.Information($"Connection reconnected: {s}");
                return Task.CompletedTask;
            };

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
