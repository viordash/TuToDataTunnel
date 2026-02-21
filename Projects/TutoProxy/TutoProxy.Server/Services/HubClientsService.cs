using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using TutoProxy.Server.Communication;
using TuToProxy.Core;
using TuToProxy.Core.CommandLine;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Services {
    public interface IHubClientsService : IAsyncDisposable {
        Task Connect(string connectionId, string? queryString);
        Task DisconnectAsync(string connectionId);
        HubClient GetClient(string connectionId);
        string GetConnectionIdForTcp(int port);
        string GetConnectionIdForUdp(int port);
    }

    public class HubClientsService : IHubClientsService {
        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, HubClient> connectedClients = new();
        protected readonly ConcurrentDictionary<int, string> tcpPortToConnectionId = new();
        protected readonly ConcurrentDictionary<int, string> udpPortToConnectionId = new();
        // Maps clientId to master connectionId
        protected readonly ConcurrentDictionary<string, string> clientIdToMasterConnectionId = new();
        // Maps worker connectionId to clientId (for cleanup)
        protected readonly ConcurrentDictionary<string, string> workerConnectionToClientId = new();
        readonly IHostApplicationLifetime applicationLifetime;
        readonly IServiceProvider serviceProvider;
        readonly IProcessMonitor processMonitor;
        readonly IPEndPoint localEndPoint;
        readonly HashSet<int>? alowedTcpPorts;
        readonly HashSet<int>? alowedUdpPorts;
        readonly HashSet<string>? alowedClients;

        public HubClientsService(
            ILogger logger,
            IHostApplicationLifetime applicationLifetime,
            IServiceProvider serviceProvider,
            IProcessMonitor processMonitor,
            IPEndPoint localEndPoint,
            IEnumerable<int>? alowedTcpPorts,
            IEnumerable<int>? alowedUdpPorts,
            IEnumerable<string>? alowedClients) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
            Guard.NotNull(serviceProvider, nameof(serviceProvider));
            Guard.NotNull(processMonitor, nameof(processMonitor));
            Guard.NotNull(localEndPoint, nameof(localEndPoint));
            Guard.NotNull(alowedTcpPorts ?? alowedUdpPorts, "alowedTcpPorts ?? alowedUdpPorts");
            this.logger = logger;
            this.applicationLifetime = applicationLifetime;
            this.serviceProvider = serviceProvider;
            this.processMonitor = processMonitor;
            this.localEndPoint = localEndPoint;
            this.alowedTcpPorts = alowedTcpPorts?.ToHashSet();
            this.alowedUdpPorts = alowedUdpPorts?.ToHashSet();
            this.alowedClients = alowedClients?.ToHashSet();
        }

        public async Task Connect(string connectionId, string? queryString) {
            if(queryString == null) {
                throw new ClientConnectionException(connectionId, "QueryString empty");
            }
            var query = QueryHelpers.ParseQuery(queryString);
            var tcpPresent = query.TryGetValue(SignalRParams.TcpQuery, out StringValues tcpQuery);
            var udpPresent = query.TryGetValue(SignalRParams.UdpQuery, out StringValues udpQuery);
            var clientIdPresent = query.TryGetValue(SignalRParams.ClientId, out StringValues clientId);
            var isWorker = query.TryGetValue(SignalRParams.WorkerConnection, out StringValues workerValue)
                && workerValue.FirstOrDefault() == "true";

            if(alowedClients != null) {
                if(!clientIdPresent) {
                    throw new ClientConnectionException(connectionId, "clientId param requried");
                }

                if(!alowedClients.Contains(clientId.FirstOrDefault()!)) {
                    throw new ClientConnectionException(clientId, connectionId, "Access denied");
                }
            }

            // Worker connections link to existing master
            if(isWorker) {
                var clientIdStr = clientId.FirstOrDefault();
                if(string.IsNullOrEmpty(clientIdStr)) {
                    throw new ClientConnectionException(connectionId, "Worker connection requires clientId");
                }

                if(!clientIdToMasterConnectionId.TryGetValue(clientIdStr, out var masterConnectionId)) {
                    throw new ClientConnectionException(clientId, connectionId, "Master connection not found");
                }

                if(!connectedClients.TryGetValue(masterConnectionId, out var masterHubClient)) {
                    throw new ClientConnectionException(clientId, connectionId, "Master HubClient not found");
                }

                // Worker shares the same HubClient as master
                if(!connectedClients.TryAdd(connectionId, masterHubClient)) {
                    throw new ClientConnectionException(clientId, connectionId, "Worker already connected");
                }

                workerConnectionToClientId.TryAdd(connectionId, clientIdStr);
                logger.Information($"Connect worker [{clientIdStr}] :{connectionId}");
                return;
            }

            // Master connection - original logic
            if(!tcpPresent && !udpPresent) {
                throw new ClientConnectionException(clientId, connectionId, "tcp or udp options requried");
            }

            List<int>? tcpPorts = null;
            if(!string.IsNullOrEmpty(tcpQuery)) {
                tcpPorts = PortsArgument.Parse(tcpQuery).Ports;
            }

            List<int>? udpPorts = null;
            if(!string.IsNullOrEmpty(udpQuery)) {
                udpPorts = PortsArgument.Parse(udpQuery).Ports;
            }

            if(tcpPorts == null && udpPorts == null) {
                throw new ClientConnectionException(clientId, connectionId, "tcp or udp options error");
            }

            if(HasBannedPorts(alowedTcpPorts, tcpPorts)) {
                throw new ClientConnectionException(clientId, connectionId, "one of tcp ports is not allowed");
            }

            if(HasBannedPorts(alowedUdpPorts, udpPorts)) {
                throw new ClientConnectionException(clientId, connectionId, "one of udp ports is not allowed");
            }

            if(tcpPorts != null) {
                foreach(var port in tcpPorts) {
                    if (!tcpPortToConnectionId.TryAdd(port, connectionId)) {
                        throw new ClientConnectionException(clientId, connectionId, $"tcp port '{port}' already used");
                    }
                }
            }
            if(udpPorts != null) {
                foreach(var port in udpPorts) {
                    if (!udpPortToConnectionId.TryAdd(port, connectionId)) {
                        throw new ClientConnectionException(clientId, connectionId, $"udp port '{port}' already used");
                    }
                }
            }

            var hubClient = new HubClient(localEndPoint, tcpPorts, udpPorts, serviceProvider);
            if(!connectedClients.TryAdd(connectionId, hubClient)) {
                await hubClient.DisposeAsync();

                if(tcpPorts != null) {
                    foreach(var port in tcpPorts) {
                        tcpPortToConnectionId.TryRemove(port, out _);
                    }
                }
                if(udpPorts != null) {
                    foreach(var port in udpPorts) {
                        udpPortToConnectionId.TryRemove(port, out _);
                    }
                }
                throw new ClientConnectionException(clientId, connectionId, "Client already connected");
            } else {
                // Register master connection for workers to find
                var clientIdStr = clientId.FirstOrDefault();
                if(!string.IsNullOrEmpty(clientIdStr)) {
                    clientIdToMasterConnectionId.TryAdd(clientIdStr, connectionId);
                }

                logger.Information($"Connect master [{(clientIdPresent ? clientIdStr : "")}] :{connectionId} (tcp:{tcpQuery}, udp:{udpQuery})");
                _ = hubClient.Listen();
                processMonitor.ConnectHubClient(connectionId, tcpPorts, udpPorts);
            }
        }

        public async Task DisconnectAsync(string connectionId) {
            // Check if this is a worker connection
            if(workerConnectionToClientId.TryRemove(connectionId, out var clientId)) {
                logger.Information($"Disconnect worker [{clientId}] :{connectionId}");
                connectedClients.TryRemove(connectionId, out _);
                return;
            }

            // Master connection
            logger.Information($"Disconnect master :{connectionId}");
            if(connectedClients.TryRemove(connectionId, out HubClient? hubClient)) {
                // Remove clientId mapping
                foreach(var kvp in clientIdToMasterConnectionId) {
                    if(kvp.Value == connectionId) {
                        clientIdToMasterConnectionId.TryRemove(kvp.Key, out _);
                        break;
                    }
                }

                if(hubClient.TcpPorts != null) {
                    foreach(var port in hubClient.TcpPorts) {
                        tcpPortToConnectionId.TryRemove(port, out _);
                    }
                }
                if(hubClient.UdpPorts != null) {
                    foreach(var port in hubClient.UdpPorts) {
                        udpPortToConnectionId.TryRemove(port, out _);
                    }
                }

                processMonitor.DisconnectHubClient(connectionId, hubClient.TcpPorts, hubClient.UdpPorts);
                await hubClient.DisposeAsync();
            }
        }

        bool HasBannedPorts(HashSet<int>? allowedPorts, List<int>? ports) {
            if(allowedPorts == null || ports == null) {
                return false;
            }
            foreach(var port in ports) {
                if(!allowedPorts.Contains(port)) {
                    return true;
                }
            }
            return false;
        }

        public async ValueTask DisposeAsync() {
            foreach(var hubClient in connectedClients.Values) {
                await hubClient.DisposeAsync();
            }
        }

        public HubClient GetClient(string connectionId) {
            if(!connectedClients.TryGetValue(connectionId, out HubClient? hubClient)) {
                throw new HubClientNotFoundException(connectionId);
            }
            return hubClient;
        }

        public string GetConnectionIdForTcp(int port) {
            if(!tcpPortToConnectionId.TryGetValue(port, out var connectionId)) {
                throw new HubClientNotFoundException(DataProtocol.Tcp, port);
            }
            return connectionId;
        }

        public string GetConnectionIdForUdp(int port) {
            if(!udpPortToConnectionId.TryGetValue(port, out var connectionId)) {
                throw new HubClientNotFoundException(DataProtocol.Udp, port);
            }
            return connectionId;
        }
    }
}
