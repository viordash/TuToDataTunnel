using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Tests.Services {
    public class HubClientsServiceTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        class TestableClientsService : HubClientsService {
            public TestableClientsService(ILogger logger, IHostApplicationLifetime applicationLifetime,
            IServiceProvider serviceProvider,
            IPEndPoint localEndPoint,
            IEnumerable<int>? alowedTcpPorts,
            IEnumerable<int>? alowedUdpPorts,
            IEnumerable<string>? alowedClients)
                : base(logger, applicationLifetime, serviceProvider, localEndPoint, alowedTcpPorts, alowedUdpPorts, alowedClients) {
            }

            public ConcurrentDictionary<string, HubClient> PublicMorozovConnectedClients {
                get { return connectedClients; }
            }
        }


        Mock<ILogger> loggerMock;
        Mock<IHostApplicationLifetime> applicationLifetimeMock;
        Mock<IServiceProvider> serviceProviderMock;
        Mock<IClientProxy> clientProxyMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        Mock<IDateTimeService> dateTimeServiceMock;
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        DateTime nowDateTime;
        string? clientsRequest;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            applicationLifetimeMock = new();
            serviceProviderMock = new();
            clientProxyMock = new();
            dataTransferServiceMock = new();
            dateTimeServiceMock = new();

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
                        _ when type == typeof(IDateTimeService) => dateTimeServiceMock.Object,
                        _ => null
                    };
                });

            nowDateTime = DateTime.Now;
            dateTimeServiceMock
                .SetupGet(x => x.Now)
                .Returns(() => nowDateTime);
        }

        [Test]
        public void Clients_WithAlready_Used_TcpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=80,81,443");
            testable.Connect("connectionId1", clientProxyMock.Object, "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "tcpquery=80"), "tcp ports already in use");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "tcpquery=180,181,1443"), "tcp ports already in use");
        }

        [Test]
        public void Clients_WithAlready_Used_UdpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);
            testable.Connect("connectionId0", clientProxyMock.Object, "udpquery=1080,1081,10443");
            testable.Connect("connectionId1", clientProxyMock.Object, "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "udpquery=1080"), "udp ports already in use");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "udpquery=10180,10181,11443"), "udp ports already in use");
        }

        [Test]
        public void Clients_With_Banned_TcpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1000, 4), Enumerable.Range(1, 65535), null);

            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=1000,1001");
            testable.Connect("connectionId1", clientProxyMock.Object, "tcpquery=1002,1003");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "tcpquery=1004"), "banned tcp ports");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "tcpquery=180,181,1443"), "banned tcp ports");
        }

        [Test]
        public void Clients_With_Banned_UdpPort_Are_Rejected_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1000, 4), null);

            testable.Connect("connectionId0", clientProxyMock.Object, "udpquery=1000,1001");
            testable.Connect("connectionId1", clientProxyMock.Object, "udpquery=1002,1003");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "udpquery=1004"), "banned udp ports");
            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId3", clientProxyMock.Object, "udpquery=180,181,1443"), "banned udp ports");
        }

        [Test]
        public void GetClient_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            testable.PublicMorozovConnectedClients.TryAdd("connectionId1", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(2000, 1).ToList(), Enumerable.Range(2000, 1), serviceProviderMock.Object));

            Assert.That(testable.GetClient("connectionId0")?.TcpPorts, Has.Member(1000));
            Assert.That(testable.GetClient("connectionId1")?.TcpPorts, Has.Member(2000));
            Assert.That(testable.GetClient("connectionId0")?.UdpPorts, Has.Member(1000));
            Assert.That(testable.GetClient("connectionId1")?.UdpPorts, Has.Member(2000));
        }

        [Test]
        public void GetClient_Throws_HubClientNotFoundException_If_No_Clients() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            Assert.Throws<HubClientNotFoundException>(() => testable.GetClient("connectionId19"));
        }

        [Test]
        public void GetConnectionIdForTcp_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            Assert.That(testable.GetConnectionIdForTcp(1000), Is.EqualTo("connectionId0"));
        }

        [Test]
        public void GetConnectionIdForTcp_Throws_HubClientNotFoundException_If_Port_Not_Bound() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            Assert.Throws<HubClientNotFoundException>(() => testable.GetConnectionIdForTcp(1001), "hub-client for Tcp(1001) not found");
        }

        [Test]
        public void GetConnectionIdForUdp_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            Assert.That(testable.GetConnectionIdForUdp(1000), Is.EqualTo("connectionId0"));
        }

        [Test]
        public void GetConnectionIdForUdp_Throws_HubClientNotFoundException_If_Port_Not_Bound() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            testable.PublicMorozovConnectedClients.TryAdd("connectionId0", new HubClient(localEndPoint, clientProxyMock.Object,
                            Enumerable.Range(1000, 1).ToList(), Enumerable.Range(1000, 1), serviceProviderMock.Object));

            Assert.Throws<HubClientNotFoundException>(() => testable.GetConnectionIdForUdp(1001), "hub-client for Udp(1001) not found");
        }

        [Test]
        public void Check_Clients_Allowing_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), new string[] { "client1", "client2" });

            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=80&clientid=client1");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0" }));

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId2", clientProxyMock.Object, "tcpquery=80&clientid=clientOther"), "client denied");
        }

        [Test]
        public void Allow_Any_Clients_Unless_ClientId_Is_Set_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=80&clientid=client1");
            testable.Connect("connectionId1", clientProxyMock.Object, "tcpquery=81&clientid=client2");
            testable.Connect("connectionId99", clientProxyMock.Object, "tcpquery=899&clientid=client99");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1", "connectionId99" }));
        }

        [Test]
        public void Throws_Error_When_ClientId_Param_Is_Set_But_Query_Wo_ClientId_Test() {
            using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), new string[] { "client1", "client2" });

            Assert.Throws<ClientConnectionException>(() => testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=80"), "clientId param requried");
        }
    }
}
