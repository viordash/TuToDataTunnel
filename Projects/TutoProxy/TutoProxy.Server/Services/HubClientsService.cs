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
        string GetConnectionIdForTcpSession(int port, int originPort);
        void RegisterTcpSession(int port, int originPort, string connectionId);
        void UnregisterTcpSession(int port, int originPort);
    }

    public class HubClientsService : IHubClientsService {
        // Represents a group of SignalR connections from the same client
        class ClientGroup {
            readonly object _lock = new();
            readonly List<string> connectionIds = new();

            public HubClient HubClient { get; }
            public IEnumerable<int>? TcpPorts { get; }
            public IEnumerable<int>? UdpPorts { get; }
            public int ExpectedConnections { get; }
            int roundRobinIndex = 0;

            public ClientGroup(HubClient hubClient, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts, int expectedConnections) {
                HubClient = hubClient;
                TcpPorts = tcpPorts;
                UdpPorts = udpPorts;
                ExpectedConnections = expectedConnections;
            }

            public void AddConnectionId(string connectionId) {
                lock(_lock) {
                    connectionIds.Add(connectionId);
                }
            }

            public bool RemoveConnectionId(string connectionId) {
                lock(_lock) {
                    return connectionIds.Remove(connectionId);
                }
            }

            public bool ContainsConnectionId(string connectionId) {
                lock(_lock) {
                    return connectionIds.Contains(connectionId);
                }
            }

            public int ConnectionCount {
                get {
                    lock(_lock) {
                        return connectionIds.Count;
                    }
                }
            }

            public string GetNextConnectionId() {
                lock(_lock) {
                    if(connectionIds.Count == 0) {
                        throw new InvalidOperationException("No connections available");
                    }
                    var index = Interlocked.Increment(ref roundRobinIndex) % connectionIds.Count;
                    return connectionIds[index];
                }
            }
        }

        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, HubClient> connectedClients = new();
        readonly ConcurrentDictionary<string, ClientGroup> clientGroups = new();  // clientGroupKey -> ClientGroup
        readonly ConcurrentDictionary<int, ClientGroup> tcpPortToClientGroup = new();
        readonly ConcurrentDictionary<int, ClientGroup> udpPortToClientGroup = new();
        readonly ConcurrentDictionary<(int port, int originPort), string> tcpSessionToConnectionId = new();
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

        static string GetClientGroupKey(string? clientId, string? tcpQuery, string? udpQuery) {
            return $"{clientId ?? ""}:{tcpQuery ?? ""}:{udpQuery ?? ""}";
        }

        public async Task Connect(string connectionId, string? queryString) {
            if(queryString == null) {
                throw new ClientConnectionException(connectionId, "QueryString empty");
            }
            var query = QueryHelpers.ParseQuery(queryString);
            var tcpPresent = query.TryGetValue(SignalRParams.TcpQuery, out StringValues tcpQuery);
            var udpPresent = query.TryGetValue(SignalRParams.UdpQuery, out StringValues udpQuery);
            var clientIdPresent = query.TryGetValue(SignalRParams.ClientId, out StringValues clientId);
            query.TryGetValue(SignalRParams.ConnectionIndex, out StringValues connIndexStr);
            query.TryGetValue(SignalRParams.TotalConnections, out StringValues totalConnStr);

            int connIndex = int.TryParse(connIndexStr.FirstOrDefault(), out var ci) ? ci : 0;
            int totalConn = int.TryParse(totalConnStr.FirstOrDefault(), out var tc) ? tc : 1;

            if(alowedClients != null) {
                if(!clientIdPresent) {
                    throw new ClientConnectionException(connectionId, "clientId param requried");
                }

                if(!alowedClients.Contains(clientId.FirstOrDefault()!)) {
                    throw new ClientConnectionException(clientId, connectionId, "Access denied");
                }
            }

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

            var groupKey = GetClientGroupKey(clientId.FirstOrDefault(), tcpQuery.FirstOrDefault(), udpQuery.FirstOrDefault());

            if(connIndex == 0) {
                // First connection - create HubClient and ClientGroup
                if(tcpPorts != null) {
                    foreach(var port in tcpPorts) {
                        if(tcpPortToClientGroup.ContainsKey(port)) {
                            throw new ClientConnectionException(clientId, connectionId, $"tcp port '{port}' already used");
                        }
                    }
                }
                if(udpPorts != null) {
                    foreach(var port in udpPorts) {
                        if(udpPortToClientGroup.ContainsKey(port)) {
                            throw new ClientConnectionException(clientId, connectionId, $"udp port '{port}' already used");
                        }
                    }
                }

                var hubClient = new HubClient(localEndPoint, tcpPorts, udpPorts, serviceProvider);
                var clientGroup = new ClientGroup(hubClient, tcpPorts, udpPorts, totalConn);
                clientGroup.AddConnectionId(connectionId);

                if(!clientGroups.TryAdd(groupKey, clientGroup)) {
                    await hubClient.DisposeAsync();
                    throw new ClientConnectionException(clientId, connectionId, "Client group already exists");
                }

                if(!connectedClients.TryAdd(connectionId, hubClient)) {
                    clientGroups.TryRemove(groupKey, out _);
                    await hubClient.DisposeAsync();
                    throw new ClientConnectionException(clientId, connectionId, "Client already connected");
                }

                // Register port mappings
                if(tcpPorts != null) {
                    foreach(var port in tcpPorts) {
                        tcpPortToClientGroup[port] = clientGroup;
                    }
                }
                if(udpPorts != null) {
                    foreach(var port in udpPorts) {
                        udpPortToClientGroup[port] = clientGroup;
                    }
                }

                logger.Information($"Connect [{(clientIdPresent ? clientId.FirstOrDefault() : "")}] :{connectionId} [0/{totalConn}] (tcp:{tcpQuery}, udp:{udpQuery})");
                _ = hubClient.Listen();
                processMonitor.ConnectHubClient(connectionId, tcpPorts, udpPorts);

            } else {
                // Additional connection - join existing ClientGroup
                // Wait for connection 0 to create the group (with timeout)
                ClientGroup? existingGroup = null;
                const int maxRetries = 50;  // 5 seconds total (50 * 100ms)
                for(int retry = 0; retry < maxRetries; retry++) {
                    if(clientGroups.TryGetValue(groupKey, out existingGroup)) {
                        break;
                    }
                    await Task.Delay(100);
                }

                if(existingGroup == null) {
                    throw new ClientConnectionException(clientId, connectionId, $"Client group not found. Connection 0 must connect first.");
                }

                existingGroup.AddConnectionId(connectionId);

                if(!connectedClients.TryAdd(connectionId, existingGroup.HubClient)) {
                    existingGroup.RemoveConnectionId(connectionId);
                    throw new ClientConnectionException(clientId, connectionId, "Client already connected");
                }

                logger.Information($"Connect [{(clientIdPresent ? clientId.FirstOrDefault() : "")}] :{connectionId} [{connIndex}/{totalConn}] (joined existing group)");
                processMonitor.ConnectHubClient(connectionId, null, null);  // Don't report ports for additional connections
            }
        }

        public async Task DisconnectAsync(string connectionId) {
            logger.Information($"Disconnect hubClient :{connectionId}");

            if(!connectedClients.TryRemove(connectionId, out HubClient? hubClient)) {
                return;
            }

            // Find and update the client group
            ClientGroup? groupToRemove = null;
            string? groupKeyToRemove = null;

            foreach(var kvp in clientGroups) {
                if(kvp.Value.ContainsConnectionId(connectionId)) {
                    kvp.Value.RemoveConnectionId(connectionId);

                    // Clean up session mappings for this connection
                    var sessionsToRemove = tcpSessionToConnectionId
                        .Where(s => s.Value == connectionId)
                        .Select(s => s.Key)
                        .ToList();
                    foreach(var session in sessionsToRemove) {
                        tcpSessionToConnectionId.TryRemove(session, out _);
                    }

                    // If this was the last connection, mark group for removal
                    if(kvp.Value.ConnectionCount == 0) {
                        groupToRemove = kvp.Value;
                        groupKeyToRemove = kvp.Key;
                    }
                    break;
                }
            }

            processMonitor.DisconnectHubClient(connectionId, null, null);

            // Clean up if last connection
            if(groupToRemove != null && groupKeyToRemove != null) {
                clientGroups.TryRemove(groupKeyToRemove, out _);

                if(groupToRemove.TcpPorts != null) {
                    foreach(var port in groupToRemove.TcpPorts) {
                        tcpPortToClientGroup.TryRemove(port, out _);
                    }
                }
                if(groupToRemove.UdpPorts != null) {
                    foreach(var port in groupToRemove.UdpPorts) {
                        udpPortToClientGroup.TryRemove(port, out _);
                    }
                }

                processMonitor.DisconnectHubClient(connectionId, groupToRemove.TcpPorts, groupToRemove.UdpPorts);
                await groupToRemove.HubClient.DisposeAsync();
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
            foreach(var group in clientGroups.Values) {
                await group.HubClient.DisposeAsync();
            }
            clientGroups.Clear();
            connectedClients.Clear();
        }

        public HubClient GetClient(string connectionId) {
            if(!connectedClients.TryGetValue(connectionId, out HubClient? hubClient)) {
                throw new HubClientNotFoundException(connectionId);
            }
            return hubClient;
        }

        public string GetConnectionIdForTcp(int port) {
            if(!tcpPortToClientGroup.TryGetValue(port, out var clientGroup)) {
                throw new HubClientNotFoundException(DataProtocol.Tcp, port);
            }
            return clientGroup.GetNextConnectionId();
        }

        public string GetConnectionIdForUdp(int port) {
            if(!udpPortToClientGroup.TryGetValue(port, out var clientGroup)) {
                throw new HubClientNotFoundException(DataProtocol.Udp, port);
            }
            return clientGroup.GetNextConnectionId();
        }

        public string GetConnectionIdForTcpSession(int port, int originPort) {
            // First check if session is already registered
            if(tcpSessionToConnectionId.TryGetValue((port, originPort), out var connId)) {
                return connId;
            }
            // Fall back to round-robin for new sessions
            return GetConnectionIdForTcp(port);
        }

        public void RegisterTcpSession(int port, int originPort, string connectionId) {
            tcpSessionToConnectionId[(port, originPort)] = connectionId;
        }

        public void UnregisterTcpSession(int port, int originPort) {
            tcpSessionToConnectionId.TryRemove((port, originPort), out _);
        }
    }
}
