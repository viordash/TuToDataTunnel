using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Tests.Services {
    public class HubClientsServiceTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        class TestableClientsService : HubClientsService {
            public TestableClientsService(ILogger logger, IHostApplicationLifetime applicationLifetime,
            IServiceProvider serviceProvider,
            IProcessMonitor processMonitor,
            IPEndPoint localEndPoint,
            IEnumerable<int>? alowedTcpPorts,
            IEnumerable<int>? alowedUdpPorts,
            IEnumerable<string>? alowedClients)
                : base(logger, applicationLifetime, serviceProvider, processMonitor, localEndPoint, alowedTcpPorts, alowedUdpPorts, alowedClients) {
            }

            public ConcurrentDictionary<string, HubClient> PublicMorozovConnectedClients {
                get { return connectedClients; }
            }
        }


        Mock<ILogger> loggerMock;
        Mock<IHostApplicationLifetime> applicationLifetimeMock;
        Mock<IServiceProvider> serviceProviderMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        Mock<IProcessMonitor> processMonitorMock;
        Mock<IServerFactory> serverFactoryMock;
        Mock<ITcpServer> tcpServerMock;
        Mock<IUdpServer> udpServerMock;
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        [SetUp]
        public void Setup() {
            loggerMock = new();
            applicationLifetimeMock = new();
            serviceProviderMock = new();
            dataTransferServiceMock = new();
            processMonitorMock = new();
            serverFactoryMock = new();
            tcpServerMock = new();
            udpServerMock = new();

            serviceProviderMock
                .Setup(x => x.GetService(It.IsAny<Type>()))
                .Returns<Type>((type) => {
                    return type switch {
                        _ when type == typeof(IDataTransferService) => dataTransferServiceMock.Object,
                        _ when type == typeof(ILogger) => loggerMock.Object,
                        _ when type == typeof(IProcessMonitor) => processMonitorMock.Object,
                        _ when type == typeof(IServerFactory) => serverFactoryMock.Object,
                        _ => null
                    };
                });

            serverFactoryMock
                .Setup(x => x.CreateTcp(It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Returns(() => {
                    return tcpServerMock.Object;
                });

            serverFactoryMock
                .Setup(x => x.CreateUdp(It.IsAny<int>(), It.IsAny<IPEndPoint>(), It.IsAny<TimeSpan>()))
                .Returns(() => {
                    return udpServerMock.Object;
                });
        }

        [Test]
        public async Task Clients_WithAlready_Used_TcpPort_Are_Rejected_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=80,81,443");
            await testable.Connect("connectionId1", "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId2", "tcpquery=80"), "tcp ports already in use");
            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId3", "tcpquery=180,181,1443"), "tcp ports already in use");
        }

        [Test]
        public async Task Clients_WithAlready_Used_UdpPort_Are_Rejected_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);
            await testable.Connect("connectionId0", "udpquery=1080,1081,10443");
            await testable.Connect("connectionId1", "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId2", "udpquery=1080"), "udp ports already in use");
            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId3", "udpquery=10180,10181,11443"), "udp ports already in use");
        }

        [Test]
        public async Task Clients_With_Banned_TcpPort_Are_Rejected_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1000, 4), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=1000,1001");
            await testable.Connect("connectionId1", "tcpquery=1002,1003");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId2", "tcpquery=1004"), "banned tcp ports");
            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId3", "tcpquery=180,181,1443"), "banned tcp ports");
        }

        [Test]
        public async Task Clients_With_Banned_UdpPort_Are_Rejected_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1000, 4), null);

            await testable.Connect("connectionId0", "udpquery=1000,1001");
            await testable.Connect("connectionId1", "udpquery=1002,1003");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId2", "udpquery=1004"), "banned udp ports");
            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId3", "udpquery=180,181,1443"), "banned udp ports");
        }

        [Test]
        public async Task GetClient_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=1000&udpquery=1000");
            await testable.Connect("connectionId1", "tcpquery=2000&udpquery=2000");

            Assert.That(testable.GetClient("connectionId0")?.TcpPorts, Has.Member(1000));
            Assert.That(testable.GetClient("connectionId1")?.TcpPorts, Has.Member(2000));
            Assert.That(testable.GetClient("connectionId0")?.UdpPorts, Has.Member(1000));
            Assert.That(testable.GetClient("connectionId1")?.UdpPorts, Has.Member(2000));
        }

        [Test]
        public async Task GetClient_Throws_HubClientNotFoundException_If_No_Clients() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            await testable.Connect("connectionId0", "tcpquery=1000&udpquery=1000");

            Assert.Throws<HubClientNotFoundException>(() => testable.GetClient("connectionId19"));
        }

        [Test]
        public async Task GetConnectionIdForTcp_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            await testable.Connect("connectionId0", "tcpquery=1000&udpquery=1000");

            Assert.That(testable.GetConnectionIdForTcp(1000), Is.EqualTo("connectionId0"));
        }

        [Test]
        public async Task GetConnectionIdForTcp_Throws_HubClientNotFoundException_If_Port_Not_Bound() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4), null);

            await testable.Connect("connectionId0", "tcpquery=1000&udpquery=1000");

            Assert.Throws<HubClientNotFoundException>(() => testable.GetConnectionIdForTcp(1001), "hub-client for Tcp(1001) not found");
        }

        [Test]
        public async Task GetConnectionIdForUdp_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=1000&udpquery=1000");

            Assert.That(testable.GetConnectionIdForUdp(1000), Is.EqualTo("connectionId0"));
        }

        [Test]
        public async Task GetConnectionIdForUdp_Throws_HubClientNotFoundException_If_Port_Not_Bound() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=1000&udpquery=1000");

            Assert.Throws<HubClientNotFoundException>(() => testable.GetConnectionIdForUdp(1001), "hub-client for Udp(1001) not found");
        }

        [Test]
        public async Task Check_Clients_Allowing_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), new string[] { "client1", "client2" });

            await testable.Connect("connectionId0", "tcpquery=80&clientid=client1");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0" }));

            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId2", "tcpquery=80&clientid=clientOther"), "client denied");
        }

        [Test]
        public async Task Allow_Any_Clients_Unless_ClientId_Is_Set_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=80&clientid=client1");
            await testable.Connect("connectionId1", "tcpquery=81&clientid=client2");
            await testable.Connect("connectionId99", "tcpquery=899&clientid=client99");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1", "connectionId99" }));
        }

        [Test]
        public async Task Throws_Error_When_ClientId_Param_Is_Set_But_Query_Wo_ClientId_Test() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), new string[] { "client1", "client2" });

            Assert.ThrowsAsync<ClientConnectionException>(() => testable.Connect("connectionId0", "tcpquery=80"), "clientId param requried");
        }

        [Test]
        [Repeat(10)]
        public async Task Concurrent_Connect_With_Same_Ports_Should_Allow_Only_One_Client() {
            const int threadCount = 20;
            const int targetPort = 5000;

            await using var testable = new TestableClientsService(
                loggerMock.Object,
                applicationLifetimeMock.Object,
                serviceProviderMock.Object,
                processMonitorMock.Object,
                localEndPoint,
                Enumerable.Range(1, 65535),
                Enumerable.Range(1, 65535),
                null);

            var barrier = new Barrier(threadCount);
            var successCount = 0;
            var exceptionCount = 0;
            var threads = new Thread[threadCount];

            for(int i = 0; i < threadCount; i++) {
                var connectionId = $"connectionId_{i}";
                threads[i] = new Thread(() => {
                    barrier.SignalAndWait(); // Синхронизируем старт всех потоков
                    try {
                        testable.Connect(connectionId, $"tcpquery={targetPort}").GetAwaiter().GetResult();
                        Interlocked.Increment(ref successCount);
                    } catch(ClientConnectionException) {
                        Interlocked.Increment(ref exceptionCount);
                    }
                });
            }

            foreach(var thread in threads) {
                thread.Start();
            }

            foreach(var thread in threads) {
                thread.Join();
            }

            // Проверяем: только ОДИН клиент должен успешно подключиться с портом 5000
            // Если race condition происходит, successCount > 1
            Assert.That(successCount, Is.EqualTo(1),
                $"Race condition detected! {successCount} clients connected with same port {targetPort}");
            Assert.That(exceptionCount, Is.EqualTo(threadCount - 1),
                $"Expected {threadCount - 1} rejections, got {exceptionCount}");

            // Дополнительная проверка: только один клиент в словаре имеет этот порт
            var clientsWithPort = testable.PublicMorozovConnectedClients.Values
                .Where(c => c.TcpPorts?.Contains(targetPort) == true)
                .Count();
            Assert.That(clientsWithPort, Is.EqualTo(1),
                $"Race condition: {clientsWithPort} clients registered with port {targetPort}");
        }

        [Test]
        public async Task Parallel_Connections_From_Same_Client_Share_HubClient() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            // First connection (index 0) creates the HubClient
            await testable.Connect("connectionId0", "tcpquery=1000&connindex=0&totalconn=4&clientid=client1");
            // Additional connections join the existing group
            await testable.Connect("connectionId1", "tcpquery=1000&connindex=1&totalconn=4&clientid=client1");
            await testable.Connect("connectionId2", "tcpquery=1000&connindex=2&totalconn=4&clientid=client1");
            await testable.Connect("connectionId3", "tcpquery=1000&connindex=3&totalconn=4&clientid=client1");

            Assert.That(testable.PublicMorozovConnectedClients.Keys.Count, Is.EqualTo(4));

            // All connections should share the same HubClient
            var hubClient0 = testable.GetClient("connectionId0");
            var hubClient1 = testable.GetClient("connectionId1");
            var hubClient2 = testable.GetClient("connectionId2");
            var hubClient3 = testable.GetClient("connectionId3");

            Assert.That(hubClient0, Is.SameAs(hubClient1));
            Assert.That(hubClient1, Is.SameAs(hubClient2));
            Assert.That(hubClient2, Is.SameAs(hubClient3));
        }

        [Test]
        public async Task GetConnectionIdForTcp_Returns_Different_Connections_Round_Robin() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            // Setup 4 parallel connections
            await testable.Connect("connectionId0", "tcpquery=1000&connindex=0&totalconn=4&clientid=client1");
            await testable.Connect("connectionId1", "tcpquery=1000&connindex=1&totalconn=4&clientid=client1");
            await testable.Connect("connectionId2", "tcpquery=1000&connindex=2&totalconn=4&clientid=client1");
            await testable.Connect("connectionId3", "tcpquery=1000&connindex=3&totalconn=4&clientid=client1");

            // Get connection IDs in round-robin fashion
            var ids = new HashSet<string>();
            for(int i = 0; i < 8; i++) {
                ids.Add(testable.GetConnectionIdForTcp(1000));
            }

            // Should have used all 4 connections
            Assert.That(ids.Count, Is.EqualTo(4));
            Assert.That(ids, Does.Contain("connectionId0"));
            Assert.That(ids, Does.Contain("connectionId1"));
            Assert.That(ids, Does.Contain("connectionId2"));
            Assert.That(ids, Does.Contain("connectionId3"));
        }

        [Test]
        public async Task TcpSession_Registration_Provides_Affinity() {
            await using var testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, serviceProviderMock.Object, processMonitorMock.Object, localEndPoint,
                Enumerable.Range(1, 65535), Enumerable.Range(1, 65535), null);

            await testable.Connect("connectionId0", "tcpquery=1000&connindex=0&totalconn=2&clientid=client1");
            await testable.Connect("connectionId1", "tcpquery=1000&connindex=1&totalconn=2&clientid=client1");

            // Register a session with specific connection
            testable.RegisterTcpSession(1000, 50000, "connectionId1");

            // GetConnectionIdForTcpSession should return the registered connection
            var connId = testable.GetConnectionIdForTcpSession(1000, 50000);
            Assert.That(connId, Is.EqualTo("connectionId1"));

            // Unregister and it should fall back to round-robin
            testable.UnregisterTcpSession(1000, 50000);
            // Now it will use round-robin (could be either)
            var connIdAfter = testable.GetConnectionIdForTcpSession(1000, 50000);
            Assert.That(connIdAfter, Does.StartWith("connectionId"));
        }
    }
}
