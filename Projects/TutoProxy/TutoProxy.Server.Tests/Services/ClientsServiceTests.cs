using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Tests.Services {
    public class ClientsServiceTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger, IHostApplicationLifetime applicationLifetime,
            IServiceProvider serviceProvider,
            IPEndPoint localEndPoint,
            IEnumerable<int>? alowedTcpPorts,
            IEnumerable<int>? alowedUdpPorts)
                : base(logger, applicationLifetime, serviceProvider, localEndPoint, alowedTcpPorts, alowedUdpPorts) {
            }

            public ConcurrentDictionary<string, Client> PublicMorozovConnectedClients {
                get { return connectedClients; }
            }
        }


        Mock<ILogger> loggerMock;
        Mock<IHostApplicationLifetime> applicationLifetimeMock;
        Mock<IServiceProvider> serviceProviderMock;
        Mock<IClientProxy> clientProxyMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);


        string? clientsRequest;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            applicationLifetimeMock = new();
            serviceProviderMock = new();
            clientProxyMock = new();
            dataTransferServiceMock = new();

            clientsRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    clientsRequest = args[0] as string;
                });

            serviceProviderMock
                .Setup(x => x.GetService(It.IsAny<Type>()))
                .Returns<Type>((type) => {
                    return type switch {
                        _ when type == typeof(IDataTransferService) => dataTransferServiceMock.Object,
                        _ when type == typeof(ILogger) => loggerMock.Object,
                        _ => null
                    };
                });
        }

        [Test]
        public void Clients_WithAlready_Used_TcpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint, Enumerable.Range(1, 65535), Enumerable.Range(1, 65535));

            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=80,81,443");
            testable.Connect("connectionId1", clientProxyMock.Object, "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "tcpquery=80"), "tcp ports already in use");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "tcpquery=180,181,1443"), "tcp ports already in use");
        }

        [Test]
        public void Clients_WithAlready_Used_UdpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint, Enumerable.Range(1, 65535), Enumerable.Range(1, 65535));
            testable.Connect("connectionId0", clientProxyMock.Object, "udpquery=1080,1081,10443");
            testable.Connect("connectionId1", clientProxyMock.Object, "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "udpquery=1080"), "udp ports already in use");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "udpquery=10180,10181,11443"), "udp ports already in use");
        }

        [Test]
        public void Clients_With_Banned_TcpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint, Enumerable.Range(1000, 4), Enumerable.Range(1, 65535));

            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=1000,1001");
            testable.Connect("connectionId1", clientProxyMock.Object, "tcpquery=1002,1003");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "tcpquery=1004"), "banned tcp ports");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "tcpquery=180,181,1443"), "banned tcp ports");
        }

        [Test]
        public void Clients_With_Banned_UdpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint, Enumerable.Range(1, 65535), Enumerable.Range(1000, 4));

            testable.Connect("connectionId0", clientProxyMock.Object, "udpquery=1000,1001");
            testable.Connect("connectionId1", clientProxyMock.Object, "udpquery=1002,1003");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "udpquery=1004"), "banned udp ports");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "udpquery=180,181,1443"), "banned udp ports");
        }


        [Test]
        public void GetUdpClient_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4));

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new Client(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            testable.PublicMorozovConnectedClients.TryAdd("connectionId1", new Client(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(2000, 1).ToList(), Enumerable.Range(2000, 1), serviceProviderMock.Object));

            Assert.IsNull(testable.GetUdpClient(1));
            Assert.That(testable.GetUdpClient(1000)?.UdpPorts, Has.Member(1000));
        }
    }
}
