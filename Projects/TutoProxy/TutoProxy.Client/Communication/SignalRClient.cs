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
        Task<string> StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, TransportProtocol protocol, CompressionMode compression, CancellationToken cancellationToken);
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

        record ConnectionInfo(HubConnection Connection, IServerHub HubProxy, IDisposable ReceiverSubscription);
        #endregion

        readonly ILogger logger;
        readonly IClientsService clientsService;
        ConnectionInfo[]? connections = null;

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

        public async Task<string> StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, TransportProtocol protocol, CompressionMode compression, CancellationToken cancellationToken) {
            Guard.NotNullOrEmpty(server, nameof(server));
            Guard.NotNull(tcpQuery ?? udpQuery, $"Tcp ?? Udp");

            await StopAsync();

            var poolSize = SignalRParams.ConnectionPoolSize;
            connections = new ConnectionInfo[poolSize];

            for(int i = 0; i < poolSize; i++) {
                var isWorker = i > 0;
                var conn = await CreateConnection(server, tcpQuery, udpQuery, clientId, protocol, compression, isWorker, cancellationToken);
                connections[i] = conn;

                // Small delay after master to ensure server registers it before workers connect
                if(!isWorker && poolSize > 1) {
                    await Task.Delay(100, cancellationToken);
                }
            }

            logger.Information($"Connection pool started ({poolSize} connections)");
            return connections[0].Connection.ConnectionId ?? "????";
        }

        async Task<ConnectionInfo> CreateConnection(string server, string? tcpQuery, string? udpQuery, string? clientId,
            TransportProtocol protocol, CompressionMode compression, bool isWorker, CancellationToken cancellationToken) {

            var ub = new UriBuilder(server);
            ub.Path = SignalRParams.Path;

            var queryParams = new List<KeyValuePair<string, string?>>();

            if(isWorker) {
                // Worker connections only need clientId and worker flag
                queryParams.Add(KeyValuePair.Create<string, string?>(SignalRParams.ClientId, clientId));
                queryParams.Add(KeyValuePair.Create<string, string?>(SignalRParams.WorkerConnection, "true"));
            } else {
                // Master connection registers ports
                queryParams.Add(KeyValuePair.Create<string, string?>(SignalRParams.TcpQuery, tcpQuery));
                queryParams.Add(KeyValuePair.Create<string, string?>(SignalRParams.UdpQuery, udpQuery));
                queryParams.Add(KeyValuePair.Create<string, string?>(SignalRParams.ClientId, clientId));
            }

            var query = QueryString.Create(queryParams);
            ub.Query = query.ToString();

            var connection = new HubConnectionBuilder()
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
                     config.SerializerOptions = BuildMessagePackOptions(compression);
                 })
                 .Build();

            var hubProxy = connection.CreateHubProxy<IServerHub>();
            var receiver = new ClientReceiver(logger, clientsService, this, StopAsync, cancellationToken);
            var receiverSubscription = connection.Register<IClientReceiver>(receiver);

            connection.Reconnecting += e => {
                logger.Warning($"Connection {(isWorker ? "worker" : "master")} lost. Reconnecting");
                return Task.CompletedTask;
            };

            connection.Reconnected += s => {
                logger.Information($"Connection {(isWorker ? "worker" : "master")} reconnected: {s}");
                return Task.CompletedTask;
            };

            await connection.StartAsync(cancellationToken);
            logger.Information($"Connection {(isWorker ? "worker" : "master")} started: {connection.ConnectionId}");

            return new ConnectionInfo(connection, hubProxy, receiverSubscription);
        }

        public async Task StopAsync() {
            if(connections != null) {
                foreach(var conn in connections) {
                    conn.ReceiverSubscription.Dispose();
                    await conn.Connection.DisposeAsync();
                }
                connections = null;
                logger.Information("Connection pool stopped");
            }
        }

        ConnectionInfo? GetConnection(int index) {
            if(connections == null || connections.Length == 0) return null;
            return connections[index % connections.Length];
        }

        ConnectionInfo? GetConnectionByHash(int port, int originPort) {
            if(connections == null || connections.Length == 0) return null;
            var hash = port ^ originPort;
            return connections[Math.Abs(hash) % connections.Length];
        }

        public async Task SendUdpResponse(UdpDataResponseModel response, CancellationToken cancellationToken) {
            var conn = GetConnectionByHash(response.Port, response.OriginPort);
            if(conn != null && conn.Connection.State == HubConnectionState.Connected) {
                await conn.HubProxy.UdpResponse(response);
            }
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            var conn = GetConnection(0); // Use master for control messages
            if(conn != null && conn.Connection.State == HubConnectionState.Connected) {
                await conn.HubProxy.DisconnectUdp(socketAddress, totalTransfered);
            }
        }

        public Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken) {
            var conn = GetConnectionByHash(response.Port, response.OriginPort);
            if(conn != null && conn.Connection.State == HubConnectionState.Connected) {
                return conn.HubProxy.TcpResponse(response);
            }
            return Task.FromResult(-1);
        }

        public Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken) {
            var conn = GetConnection(0); // Use master for control messages
            if(conn != null && conn.Connection.State == HubConnectionState.Connected) {
                return conn.HubProxy.DisconnectTcp(socketAddress);
            }
            return Task.FromResult(false);
        }

        static MessagePackSerializerOptions BuildMessagePackOptions(CompressionMode compression) {
            var options = MessagePackSerializerOptions.Standard
                .WithResolver(StandardResolver.Instance)
                .WithSecurity(MessagePackSecurity.UntrustedData);

            return compression switch {
                CompressionMode.Lz4_256 => options.WithCompression(MessagePackCompression.Lz4BlockArray).WithCompressionMinLength(256),
                CompressionMode.Lz4_512 => options.WithCompression(MessagePackCompression.Lz4BlockArray).WithCompressionMinLength(512),
                CompressionMode.Lz4_1024 => options.WithCompression(MessagePackCompression.Lz4BlockArray).WithCompressionMinLength(1024),
                _ => options
            };
        }
    }
}
