﻿using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using TutoProxy.Server.Communication;
using TuToProxy.Core;
using TuToProxy.Core.CommandLine;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Services {
    public interface IHubClientsService : IDisposable {
        void Connect(string connectionId, IClientProxy clientProxy, string? queryString);
        void Disconnect(string connectionId);
        HubClient GetClient(string connectionId);
        string GetConnectionIdForTcp(int port);
        string GetConnectionIdForUdp(int port);
    }

    public class HubClientsService : IHubClientsService {
        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, HubClient> connectedClients = new();
        readonly IHostApplicationLifetime applicationLifetime;
        readonly IServiceProvider serviceProvider;
        readonly IProcessMonitor processMonitor;
        readonly IPEndPoint localEndPoint;
        readonly IEnumerable<int>? alowedTcpPorts;
        readonly IEnumerable<int>? alowedUdpPorts;
        readonly IEnumerable<string>? alowedClients;

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
            this.alowedTcpPorts = alowedTcpPorts;
            this.alowedUdpPorts = alowedUdpPorts;
            this.alowedClients = alowedClients;
        }

        public void Connect(string connectionId, IClientProxy clientProxy, string? queryString) {
            if(queryString == null) {
                throw new ClientConnectionException(connectionId, "QueryString empty");
            }
            var query = QueryHelpers.ParseQuery(queryString);
            var tcpPresent = query.TryGetValue(SignalRParams.TcpQuery, out StringValues tcpQuery);
            var udpPresent = query.TryGetValue(SignalRParams.UdpQuery, out StringValues udpQuery);
            var clientIdPresent = query.TryGetValue(SignalRParams.ClientId, out StringValues clientId);

            if(alowedClients != null) {
                if(!clientIdPresent) {
                    throw new ClientConnectionException(connectionId, "clientId param requried");
                }

                if(!alowedClients.Contains(clientId.FirstOrDefault())) {
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


            var bannedTcpPorts = GetBannedPorts(alowedTcpPorts, tcpPorts);
            if(bannedTcpPorts.Any()) {
                var message = $"banned tcp ports [{string.Join(",", bannedTcpPorts)}]";
                throw new ClientConnectionException(clientId, connectionId, message);
            }

            var bannedUdpPorts = GetBannedPorts(alowedUdpPorts, udpPorts);
            if(bannedUdpPorts.Any()) {
                var message = $"banned udp ports [{string.Join(",", bannedUdpPorts)}]";
                throw new ClientConnectionException(clientId, connectionId, message);
            }

            var hubClients = connectedClients.Values.ToList();

            var alreadyUsedTcpPorts = GetAlreadyUsedTcpPorts(hubClients, tcpPorts);
            if(alreadyUsedTcpPorts.Any()) {
                var message = $"tcp ports already in use [{string.Join(",", alreadyUsedTcpPorts)}]";
                throw new ClientConnectionException(clientId, connectionId, message);
            }

            var alreadyUsedUdpPorts = GetAlreadyUsedUdpPorts(hubClients, udpPorts);
            if(alreadyUsedUdpPorts.Any()) {
                var message = $"udp ports already in use [{string.Join(",", alreadyUsedUdpPorts)}]";
                throw new ClientConnectionException(clientId, connectionId, message);
            }

            var hubClient = new HubClient(localEndPoint, clientProxy, tcpPorts, udpPorts, serviceProvider);
            if(connectedClients.TryAdd(connectionId, hubClient)) {
                logger.Information($"Connect [{(clientIdPresent ? clientId.FirstOrDefault() : "")}] :{connectionId} (tcp:{tcpQuery}, udp:{udpQuery})");
                hubClient.Listen();
                processMonitor.ConnectHubClient(connectionId, tcpPorts, udpPorts);
            }
        }

        public void Disconnect(string connectionId) {
            logger.Information($"Disconnect hubClient :{connectionId}");
            if(connectedClients.TryRemove(connectionId, out HubClient? hubClient)) {
                processMonitor.DisconnectHubClient(connectionId, hubClient.TcpPorts, hubClient.UdpPorts);
                hubClient.Dispose();
            }
        }

        IEnumerable<int> GetBannedPorts(IEnumerable<int>? allowedPorts, IEnumerable<int>? ports) {
            if(allowedPorts != null && ports != null) {
                var bannedPorts = ports
                .Where(x => !allowedPorts.Contains(x));
                return bannedPorts;
            }
            return Enumerable.Empty<int>();
        }

        IEnumerable<int> GetAlreadyUsedTcpPorts(List<HubClient> hubClients, IEnumerable<int>? tcpPorts) {
            if(tcpPorts != null) {
                var alreadyUsedPorts = hubClients
                .Where(x => x.TcpPorts != null)
                .SelectMany(x => x.TcpPorts!)
                .Intersect(tcpPorts);
                return alreadyUsedPorts;
            }
            return Enumerable.Empty<int>();
        }

        IEnumerable<int> GetAlreadyUsedUdpPorts(List<HubClient> hubClients, IEnumerable<int>? udpPorts) {
            if(udpPorts != null) {
                var alreadyUsedPorts = hubClients
                .Where(x => x.UdpPorts != null)
                .SelectMany(x => x.UdpPorts!)
                .Intersect(udpPorts);
                return alreadyUsedPorts;
            }
            return Enumerable.Empty<int>();
        }

        public void Dispose() {
            var hubClients = connectedClients.Values.ToList();
            foreach(var hubClient in hubClients) {
                hubClient.Dispose();
            }
        }

        public HubClient GetClient(string connectionId) {
            if(!connectedClients.TryGetValue(connectionId, out HubClient? hubClient)) {
                throw new HubClientNotFoundException(connectionId);
            }
            return hubClient;
        }

        public string GetConnectionIdForTcp(int port) {
            var hubClients = connectedClients.ToList();
            var connectionId = hubClients
                .Where(x => x.Value.TcpPorts?.Contains(port) == true)
                .Select(x => x.Key)
                .FirstOrDefault();
            if(connectionId == null) {
                throw new HubClientNotFoundException(DataProtocol.Tcp, port);
            }
            return connectionId;
        }

        public string GetConnectionIdForUdp(int port) {
            var hubClients = connectedClients.ToList();
            var connectionId = hubClients
                .Where(x => x.Value.UdpPorts?.Contains(port) == true)
                .Select(x => x.Key)
                .FirstOrDefault();
            if(connectionId == null) {
                throw new HubClientNotFoundException(DataProtocol.Udp, port);
            }
            return connectionId;
        }
    }
}
