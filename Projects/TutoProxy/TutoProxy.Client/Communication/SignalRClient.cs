using System.CommandLine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Communication {
    public interface ISignalRClient {
        Task StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, CancellationToken cancellationToken);
        Task StopAsync();
        Task SendTcpResponse(TransferTcpResponseModel response, CancellationToken cancellationToken);
        Task SendTcpCommand(TransferTcpCommandModel command, CancellationToken cancellationToken);
        Task SendUdpResponse(TransferUdpResponseModel response, CancellationToken cancellationToken);
        Task SendUdpCommand(TransferUdpCommandModel command, CancellationToken cancellationToken);
    }

    internal class SignalRClient : ISignalRClient {
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
        readonly IDataExchangeService dataExchangeService;
        HubConnection? connection = null;

        public SignalRClient(
                ILogger logger,
                IDataExchangeService dataExchangeService
                ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataExchangeService, nameof(dataExchangeService));
            this.logger = logger;
            this.dataExchangeService = dataExchangeService;
        }

        public async Task StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, CancellationToken cancellationToken) {
            Guard.NotNullOrEmpty(server, nameof(server));
            Guard.NotNull(tcpQuery ?? udpQuery, $"Tcp ?? Udp");

            await StopAsync();

            var ub = new UriBuilder(server);
            ub.Path = SignalRParams.Path;

            var query = QueryString.Create(new[] {
                KeyValuePair.Create(SignalRParams.TcpQuery, tcpQuery),
                KeyValuePair.Create(SignalRParams.UdpQuery, udpQuery),
                KeyValuePair.Create(SignalRParams.ClientId, clientId)
            });
            ub.Query = query.ToString();

            connection = new HubConnectionBuilder()
                 .WithUrl(ub.Uri)
                 .WithAutomaticReconnect(new RetryPolicy(logger))
                 .Build();

            connection.On<TransferTcpRequestModel>("TcpRequest", async (request) => {
                await dataExchangeService.HandleTcpRequest(request, this, cancellationToken);
            });

            connection.On<TransferTcpCommandModel>("TcpCommand", async (command) => {
                await dataExchangeService.HandleTcpCommand(command, this, cancellationToken);
            });

            connection.On<TransferUdpRequestModel>("UdpRequest", async (request) => {
                await dataExchangeService.HandleUdpRequest(request, this, cancellationToken);
            });

            connection.On<TransferUdpCommandModel>("UdpCommand", async (command) => {
                await dataExchangeService.HandleUdpCommand(command, this, cancellationToken);
            });

            connection.On<string>("Errors", async (message) => {
                logger.Error(message);
                await StopAsync();
            });


            connection.On<TransferTcpRequestModel>("CreateStream", async (request) => {
                var channel = await connection.StreamAsChannelAsync<byte[]>("TcpStream", request.Payload.Port, request.Payload.OriginPort, cancellationToken);
                await dataExchangeService.CreateStream(request, channel, this, cancellationToken);
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

        public async Task StopAsync() {
            if(connection != null) {
                await connection.DisposeAsync();
                logger.Information("Connection stopped");
                connection = null;
            }
        }

        public async Task SendTcpResponse(TransferTcpResponseModel response, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.InvokeAsync("TcpResponse", response, cancellationToken);
            }
        }

        public async Task SendTcpCommand(TransferTcpCommandModel command, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.InvokeAsync("TcpCommand", command, cancellationToken);
            }
        }

        public async Task SendUdpResponse(TransferUdpResponseModel response, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.InvokeAsync("UdpResponse", response, cancellationToken);
            }
        }

        public async Task SendUdpCommand(TransferUdpCommandModel command, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.InvokeAsync("UdpCommand", command, cancellationToken);
            }
        }




        //public IAsyncEnumerable<TcpDataModel> GetTcpStreamToClient(int port, int originPort, CancellationToken cancellationToken) {
        //    if(connection?.State != HubConnectionState.Connected) {
        //        throw new HubConnectionException(connection?.ConnectionId);
        //    }
        //    var stream = connection.StreamAsync<TcpDataModel>("TcpStreamToClient", port, originPort, cancellationToken);
        //    return stream;
        //}

        //public async Task CreateTcpStreamToHub(IAsyncEnumerable<TcpDataModel> stream, CancellationToken cancellationToken) {
        //    if(connection?.State != HubConnectionState.Connected) {
        //        throw new HubConnectionException(connection?.ConnectionId);
        //    }
        //    await connection.SendAsync("TcpStreamToHub", stream);
        //}

        //public async Task<IAsyncEnumerable<TcpDataModel>> CreateTcpStream(int port, int originPort, IAsyncEnumerable<TcpDataModel> stream, CancellationToken cancellationToken) {
        //    if(connection?.State != HubConnectionState.Connected) {
        //        throw new HubConnectionException(connection?.ConnectionId);
        //    }
        //    await connection.SendAsync("TcpStreamToHub", stream);
        //    return GetTcpStreamToClient(port, originPort, cancellationToken);
        //}
    }
}
