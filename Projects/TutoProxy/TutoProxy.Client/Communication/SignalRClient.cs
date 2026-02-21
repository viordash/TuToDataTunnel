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
        Task<string> StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, TransportProtocol protocol, int parallelCount, CancellationToken cancellationToken);
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

        class SignalRConnection : ITcpDataChannel, IAsyncDisposable {
            readonly HubConnection connection;
            readonly IServerHub hubProxy;
            readonly IDisposable receiverSubscription;

            public int Index { get; }

            public SignalRConnection(int index, HubConnection connection, IClientReceiver receiver) {
                Index = index;
                this.connection = connection;
                hubProxy = connection.CreateHubProxy<IServerHub>();
                receiverSubscription = connection.Register<IClientReceiver>(receiver);
            }

            public async ValueTask DisposeAsync() {
                receiverSubscription.Dispose();
                await connection.DisposeAsync();
            }

            public Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
                if(connection.State == HubConnectionState.Connected) {
                    return hubProxy.TcpResponse(response);
                }
                return Task.FromResult(-1);
            }

            public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
                if(connection.State == HubConnectionState.Connected) {
                    return hubProxy.DisconnectTcp(socketAddress);
                }
                return Task.FromResult(false);
            }

            public Task SendUdpResponse(UdpDataResponseModel response) {
                if(connection.State == HubConnectionState.Connected) {
                    return hubProxy.UdpResponse(response);
                }
                return Task.CompletedTask;
            }

            public Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
                if(connection.State == HubConnectionState.Connected) {
                    return hubProxy.DisconnectUdp(socketAddress, totalTransfered);
                }
                return Task.CompletedTask;
            }
        }

        // Wrapper that allows setting the actual channel after construction
        class TcpChannelWrapper : ITcpDataChannel {
            public ITcpDataChannel? Channel { get; set; }

            public Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
                return Channel?.SendTcpResponse(response, cancellationToken) ?? Task.FromResult(-1);
            }

            public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
                return Channel?.DisconnectTcp(socketAddress, cancellationToken) ?? Task.FromResult(false);
            }
        }

        class ClientReceiver : IClientReceiver {
            readonly ILogger logger;
            readonly IClientsService clientsService;
            readonly ISignalRClient signalRClient;
            readonly ITcpDataChannel tcpChannel;
            readonly Func<Task> stopAsync;
            readonly CancellationToken cancellationToken;

            public ClientReceiver(ILogger logger, IClientsService clientsService, ISignalRClient signalRClient,
                    ITcpDataChannel tcpChannel, Func<Task> stopAsync, CancellationToken cancellationToken) {
                this.logger = logger;
                this.clientsService = clientsService;
                this.signalRClient = signalRClient;
                this.tcpChannel = tcpChannel;
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
                logger.Debug("HandleDisconnectUdp :{socketAddress}, {totalTransfered}", socketAddress, totalTransfered);
                var client = clientsService.ObtainUdpClient(socketAddress.Port, socketAddress.OriginPort, signalRClient);
                client.Disconnect(totalTransfered);
                return Task.CompletedTask;
            }

            public async Task<SocketError> ConnectTcp(SocketAddressModel socketAddress) {
                logger.Debug("HandleConnectTcp :{socketAddress}", socketAddress);
                var client = clientsService.AddTcpClient(socketAddress.Port, socketAddress.OriginPort, signalRClient, tcpChannel);
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
        readonly List<SignalRConnection> connections = new();
        int udpConnectionIndex = 0;

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

        public async Task<string> StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, TransportProtocol protocol, int parallelCount, CancellationToken cancellationToken) {
            Guard.NotNullOrEmpty(server, nameof(server));
            Guard.NotNull(tcpQuery ?? udpQuery, $"Tcp ?? Udp");

            await StopAsync();

            var ub = new UriBuilder(server);
            ub.Path = SignalRParams.Path;

            string? firstConnectionId = null;

            for(int i = 0; i < parallelCount; i++) {
                var query = QueryString.Create(new[] {
                    KeyValuePair.Create(SignalRParams.TcpQuery, tcpQuery ?? ""),
                    KeyValuePair.Create(SignalRParams.UdpQuery, udpQuery ?? ""),
                    KeyValuePair.Create(SignalRParams.ClientId, clientId ?? ""),
                    KeyValuePair.Create(SignalRParams.ConnectionIndex, i.ToString()),
                    KeyValuePair.Create(SignalRParams.TotalConnections, parallelCount.ToString())
                });
                ub.Query = query.ToString();

                var hubConnection = new HubConnectionBuilder()
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

                // Use wrapper to break circular dependency
                var channelWrapper = new TcpChannelWrapper();
                var receiver = new ClientReceiver(logger, clientsService, this, channelWrapper, StopAsync, cancellationToken);
                var signalRConn = new SignalRConnection(i, hubConnection, receiver);
                channelWrapper.Channel = signalRConn;
                connections.Add(signalRConn);

                var connIndex = i;
                hubConnection.Reconnecting += e => {
                    logger.Warning($"Connection[{connIndex}] lost. Reconnecting");
                    return Task.CompletedTask;
                };

                hubConnection.Reconnected += s => {
                    logger.Information($"Connection[{connIndex}] reconnected: {s}");
                    return Task.CompletedTask;
                };

                await hubConnection.StartAsync(cancellationToken);
                logger.Information($"Connection[{i}] started: {hubConnection.ConnectionId}");

                if(i == 0) {
                    firstConnectionId = hubConnection.ConnectionId;
                }
            }

            logger.Information($"All {parallelCount} connections started");
            return firstConnectionId ?? "????";
        }

        public async Task StopAsync() {
            if(connections.Count == 0) {
                return;
            }
            foreach(var conn in connections) {
                await conn.DisposeAsync();
            }
            connections.Clear();
            logger.Information("All connections stopped");
        }

        public async Task SendUdpResponse(UdpDataResponseModel response, CancellationToken cancellationToken) {
            if(connections.Count > 0) {
                var index = Interlocked.Increment(ref udpConnectionIndex) % connections.Count;
                await connections[index].SendUdpResponse(response);
            }
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            if(connections.Count > 0) {
                // Use first connection for UDP disconnect (it's a control message)
                await connections[0].DisconnectUdp(socketAddress, totalTransfered);
            }
        }

        public Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
            // This method is kept for interface compatibility but should not be used for TCP
            // TCP responses go through ITcpDataChannel (SignalRConnection) directly
            if(connections.Count > 0) {
                return connections[0].SendTcpResponse(response, cancellationToken);
            }
            return Task.FromResult(-1);
        }

        public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            // This method is kept for interface compatibility but should not be used for TCP
            // TCP disconnects go through ITcpDataChannel (SignalRConnection) directly
            if(connections.Count > 0) {
                return connections[0].DisconnectTcp(socketAddress, cancellationToken);
            }
            return Task.FromResult(false);
        }
    }
}
