using System.CommandLine;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Client.Services;
using TuToProxy.Core;

namespace TutoProxy.Client.Communication {
    public interface ISignalRClient : IDisposable {
        Task StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, CancellationToken cancellationToken);
        Task StopAsync();
        Task SendUdpResponse(UdpDataResponseModel response, CancellationToken cancellationToken);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken);

        Task SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken);
        Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken);
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
        readonly IClientsService clientsService;
        HubConnection? connection = null;

        public SignalRClient(
                ILogger logger,
                IDataExchangeService dataExchangeService,
                IClientsService clientsService
                ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataExchangeService, nameof(dataExchangeService));
            this.logger = logger;
            this.dataExchangeService = dataExchangeService;
            this.clientsService = clientsService;
        }

        public void Dispose() {
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
                 .AddMessagePackProtocol(config => {
                     StaticCompositeResolver.Instance.Register(
                        MessagePack.Resolvers.StandardResolver.Instance
                    );
                     config.SerializerOptions = MessagePackSerializerOptions.Standard
                            .WithResolver(StaticCompositeResolver.Instance)
                            .WithSecurity(MessagePackSecurity.UntrustedData);
                 })
                 .Build();

            connection.On<UdpDataRequestModel>("UdpRequest", async (request) => {
                await dataExchangeService.HandleUdpRequest(request, this, cancellationToken);
            });

            connection.On<SocketAddressModel, Int64>("DisconnectUdp", (socketAddress, totalTransfered) => {
                logger.Debug($"HandleDisconnectUdp :{socketAddress}, {totalTransfered}");
                var client = clientsService.ObtainUdpClient(socketAddress.Port, socketAddress.OriginPort, this);
                client.Disconnect(totalTransfered);
            });

            connection.On<SocketAddressModel, bool>("ConnectTcp", async (socketAddress) => {
                logger.Debug($"HandleConnectTcp :{socketAddress}");
                var client = clientsService.ObtainTcpClient(socketAddress.Port, socketAddress.OriginPort, this);
                return await client.Connect(cancellationToken);
            });

            connection.On<TcpDataRequestModel>("TcpRequest", async (request) => {
                var client = clientsService.ObtainTcpClient(request.Port, request.OriginPort, this);
                await client.SendRequest(request.Data, cancellationToken);
            });

            connection.On<SocketAddressModel, Int64>("DisconnectTcp", (socketAddress, totalTransfered) => {
                logger.Debug($"HandleDisconnectTcp :{socketAddress}, {totalTransfered}");
                var client = clientsService.ObtainTcpClient(socketAddress.Port, socketAddress.OriginPort, this);
                client.Disconnect(totalTransfered, cancellationToken);
            });

            connection.On<string>("Errors", async (message) => {
                logger.Error(message);
                await StopAsync();
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

        public async Task SendUdpResponse(UdpDataResponseModel response, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.SendAsync("UdpResponse", response, cancellationToken);
            }
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.SendAsync("DisconnectUdp", socketAddress, totalTransfered, cancellationToken);
            }
        }

        public async Task SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.SendAsync("TcpResponse", response, cancellationToken);
            }
        }

        public async Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.SendAsync("DisconnectTcp", socketAddress, totalTransfered, cancellationToken);
            }
        }
    }
}
