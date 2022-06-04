﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IClientsService {
        void Start(IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts);
        UdpConnection GetUdpConnection(int port);
        void Stop();
    }

    internal class ClientsService : IClientsService {
        readonly ILogger logger;
        readonly List<NetConnection> tcpConnections = new();
        readonly List<UdpConnection> udpConnections = new();

        public ClientsService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public void Start(IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts) {
            Stop();

            if(udpPorts != null) {
                udpConnections.AddRange(udpPorts.Select(x => new UdpConnection(new IPEndPoint(IPAddress.Loopback, x), logger)));
            }
        }

        public UdpConnection GetUdpConnection(int port) {
            var connection = udpConnections.FirstOrDefault(x => x.Port == port);
            if(connection == null) {
                throw new ClientNotFoundException(DataProtocol.Udp, port);
            }
            return connection;
        }

        public void Stop() {
            foreach(var connection in tcpConnections) {
                connection.Dispose();
            }
            tcpConnections.Clear();

            foreach(var connection in udpConnections) {
                connection.Dispose();
            }
            udpConnections.Clear();
        }
    }
}