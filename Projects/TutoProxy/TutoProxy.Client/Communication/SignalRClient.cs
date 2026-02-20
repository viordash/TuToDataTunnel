using System.CommandLine;
using System.Net.Sockets;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Models;
using TypedSignalR.Client;

namespace TutoProxy.Client.Communication {
    public interface ISignalRClient : IDisposable {
        Task<string> StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, TransportProtocol protocol, CancellationToken cancellationToken);
        Task StopAsync();
        Task SendUdpResponse(UdpDataResponseModel response, CancellationToken cancellationToken);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken);

        Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken);
        Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
    }

    public class SignalRClient : ISignalRClient {
        #region inner classes
        class RetryPolicy : IRetryPolicy {
            readonly ILogger logger;
            public RetryPolicy(ILogger logger) {
                this.logger = logger;
            }
            public TimeSpan? NextRetryDelay(RetryContext retryContext) {
                if(retryContext.RetryReason?.Message != null) {
                    logger.Warning(retryContext.RetryReason.Message);
                }
                return TimeSpan.FromSeconds(Math.Min(retryContext.PreviousRetryCount + 1, 60));
            }
        }

        class ClientReceiver : IClientReceiver {
            readonly ILogger logger;
            readonly IClientsService clientsService;
            readonly ISignalRClient signalRClient;
            readonly Func<Task> stopAsync;
            readonly CancellationToken cancellationToken;

            public ClientReceiver(ILogger logger, IClientsService clientsService, ISignalRClient signalRClient,
                    Func<Task> stopAsync, CancellationToken cancellationToken) {
                this.logger = logger;
                this.clientsService = clientsService;
                this.signalRClient = signalRClient;
                this.stopAsync = stopAsync;
                this.cancellationToken = cancellationToken;
            }

            public async Task Errors(string message) {
                logger.Error(message);
                await stopAsync();
            }

            public async Task UdpRequest(UdpDataRequestModel request) {
                var client = clientsService.ObtainUdpClient(request.Port, request.OriginPort, signalRClient);
                await client.SendRequest(request.Data, cancellationToken);
                if(!client.Listening) {
                    client.Listen(request, signalRClient, cancellationToken);
                }
            }

            public Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
                logger.Debug($"HandleDisconnectUdp :{socketAddress}, {totalTransfered}");
                var client = clientsService.ObtainUdpClient(socketAddress.Port, socketAddress.OriginPort, signalRClient);
                client.Disconnect(totalTransfered);
                return Task.CompletedTask;
            }

            public async Task<SocketError> ConnectTcp(SocketAddressModel socketAddress) {
                logger.Debug($"HandleConnectTcp :{socketAddress}");
                var client = clientsService.AddTcpClient(socketAddress.Port, socketAddress.OriginPort, signalRClient);
                var result = await client.Connect(cancellationToken);
                if(result != SocketError.Success) {
                    await client.DisconnectAsync();
                }
                return result;
            }

            public async Task<int> TcpRequest(TcpDataRequestModel request) {
                if(!clientsService.ObtainTcpClient(request.Port, request.OriginPort, out TcpClient? client)) {
                    return -1;
                }
                return await client!.SendRequest(request.Data, cancellationToken);
            }

            public async Task<bool> DisconnectTcp(SocketAddressModel socketAddress) {
                if(!clientsService.ObtainTcpClient(socketAddress.Port, socketAddress.OriginPort, out TcpClient? client)) {
                    return true;
                }
                return await client!.DisconnectAsync();
            }
        }
        #endregion

        readonly ILogger logger;
        readonly IClientsService clientsService;
        HubConnection? connection = null;
        IServerHub? hubProxy = null;
        IDisposable? receiverSubscription = null;

        public SignalRClient(
                ILogger logger,
                IClientsService clientsService
                ) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
            this.clientsService = clientsService;
        }

        public void Dispose() {
        }

        public async Task<string> StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, TransportProtocol protocol, CancellationToken cancellationToken) {
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
                 .WithUrl(ub.Uri, options => {
                     if(protocol == TransportProtocol.WebSocket) {
                         options.Transports = HttpTransportType.WebSockets;
                         options.SkipNegotiation = true;
                     } else if(protocol == TransportProtocol.Http) {
                         options.Transports = HttpTransportType.LongPolling;
                     }
                 })
                 .WithAutomaticReconnect(new RetryPolicy(logger))
                 .AddMessagePackProtocol(config => {
                     config.SerializerOptions = MessagePackSerializerOptions.Standard
                            .WithResolver(StandardResolver.Instance)
                            .WithSecurity(MessagePackSecurity.UntrustedData);
                 })
                 .Build();

            hubProxy = connection.CreateHubProxy<IServerHub>();
            var receiver = new ClientReceiver(logger, clientsService, this, StopAsync, cancellationToken);
            receiverSubscription = connection.Register<IClientReceiver>(receiver);

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
            return connection.ConnectionId ?? "????";
        }

        public async Task StopAsync() {
            receiverSubscription?.Dispose();
            receiverSubscription = null;
            hubProxy = null;
            if(connection != null) {
                await connection.DisposeAsync();
                logger.Information("Connection stopped");
                connection = null;
            }
        }

        public async Task SendUdpResponse(UdpDataResponseModel response, CancellationToken cancellationToken) {
            if(hubProxy != null && connection?.State == HubConnectionState.Connected) {
                await hubProxy.UdpResponse(response);
            }
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            if(hubProxy != null && connection?.State == HubConnectionState.Connected) {
                await hubProxy.DisconnectUdp(socketAddress, totalTransfered);
            }
        }

        public Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
            if(hubProxy != null && connection?.State == HubConnectionState.Connected) {
                return hubProxy.TcpResponse(response);
            }
            return Task.FromResult(-1);
        }

        public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            if(hubProxy != null && connection?.State == HubConnectionState.Connected) {
                return hubProxy.DisconnectTcp(socketAddress);
            }
            return Task.FromResult(false);
        }
    }
}
