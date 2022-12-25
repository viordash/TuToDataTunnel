using System.Collections.Concurrent;
using System.Net;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TuToProxy.Core.Models;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Tests.Services {
    public class TcpClientsServiceTests {
        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger, IClientFactory clientFactory, IProcessMonitor processMonitor)
                : base(logger, clientFactory, processMonitor) {
            }

            public ConcurrentDictionary<int, ConcurrentDictionary<int, TcpClient>> PublicMorozovTcpClients {
                get { return tcpClients; }
            }
        }

        Mock<ILogger> loggerMock;
        Mock<IClientFactory> clientFactoryMock;
        Mock<IClientsService> clientsServiceMock;
        Mock<ISignalRClient> signalRClientMock;
        Mock<IProcessMonitor> processMonitorMock;

        TestableClientsService testable;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            clientFactoryMock = new();
            clientsServiceMock = new();
            signalRClientMock = new();
            processMonitorMock = new();

            clientFactoryMock
                .Setup(x => x.CreateTcp(It.IsAny<IPAddress>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IClientsService>(), It.IsAny<ISignalRClient>(), It.IsAny<IProcessMonitor>()))
                .Returns<IPAddress, int, int, IClientsService, ISignalRClient, IProcessMonitor>((localIpAddress, port, originPort, clientsService, dataTunnelClient, processMonitor) => {
                    return new TcpClient(new IPEndPoint(localIpAddress, port), originPort, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object,
                        processMonitorMock.Object);
                });

            testable = new TestableClientsService(loggerMock.Object, clientFactoryMock.Object, processMonitorMock.Object);
        }

        [Test]
        public void AddTcpClient_With_Banned_TcpPort_Are_Throws_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1000, 4).ToList(), Enumerable.Range(1, 65535).ToList());

            Assert.Throws<ClientNotFoundException>(() => testable.AddTcpClient(999, 50999, signalRClientMock.Object));
            Assert.Throws<ClientNotFoundException>(() => testable.AddTcpClient(1005, 51005, signalRClientMock.Object));
        }

        [Test]
        public void AddTcpClient_Only_Once_Creating_List_With_Same_Port_Clients_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.AddTcpClient(1000, 51000, signalRClientMock.Object);
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000][51000], Is.SameAs(client0));

            var client1 = testable.AddTcpClient(1000, 51001, signalRClientMock.Object);
            Assert.IsNotNull(client1);
            Assert.That(client1.Port, Is.EqualTo(1000));
            Assert.That(client1.OriginPort, Is.EqualTo(51001));

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000, 51001 }));
            Assert.That(testable.PublicMorozovTcpClients[1000][51001], Is.SameAs(client1));
        }

        [Test]
        public void AddTcpClient_Only_Once_Creating_Same_Port_Client_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.AddTcpClient(1000, 51000, signalRClientMock.Object);
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            var client1 = testable.AddTcpClient(1000, 51000, signalRClientMock.Object);
            Assert.That(client1, Is.SameAs(client0));

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000][51000], Is.SameAs(client0));
        }

        [Test]
        public void ObtainTcpClient_With_Banned_TcpPort_Are_Throws_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1000, 4).ToList(), Enumerable.Range(1, 65535).ToList());

            Assert.Throws<ClientNotFoundException>(() => testable.ObtainTcpClient(999, 50999, out _));
            Assert.Throws<ClientNotFoundException>(() => testable.ObtainTcpClient(1005, 51005, out _));
        }

        [Test]
        public void ObtainTcpClient_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1000, 4).ToList(), Enumerable.Range(1, 65535).ToList());

            var client0 = testable.AddTcpClient(1000, 51000, signalRClientMock.Object);
            var client1 = testable.AddTcpClient(1000, 51001, signalRClientMock.Object);

            Assert.That(testable.ObtainTcpClient(1000, 50999, out _), Is.False);

            Assert.That(testable.ObtainTcpClient(1000, 51000, out TcpClient? tcpClient0), Is.True);
            Assert.IsNotNull(tcpClient0);
            Assert.That(tcpClient0, Is.SameAs(client0));

            Assert.That(testable.ObtainTcpClient(1000, 51001, out TcpClient? tcpClient1), Is.True);
            Assert.IsNotNull(tcpClient1);
            Assert.That(tcpClient1, Is.SameAs(client1));
        }

        [Test]
        public async Task RemoveTcpClient_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1000, 4).ToList(), Enumerable.Range(1, 65535).ToList());

            Assert.IsNotNull(testable.AddTcpClient(1000, 51000, signalRClientMock.Object));
            Assert.IsNotNull(testable.AddTcpClient(1000, 51001, signalRClientMock.Object));

            Assert.That(await testable.RemoveTcpClient(1000, 50999), Is.False);
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000, 51001 }));

            Assert.That(await testable.RemoveTcpClient(1000, 51001), Is.True);
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));

            Assert.That(await testable.RemoveTcpClient(1000, 51000), Is.True);
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.Empty);
        }

        [Test]
        public void Stop_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1000, 4).ToList(), Enumerable.Range(1, 65535).ToList());

            Assert.IsNotNull(testable.AddTcpClient(1000, 51000, signalRClientMock.Object));
            Assert.IsNotNull(testable.AddTcpClient(1001, 51001, signalRClientMock.Object));

            testable.Stop();

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.Empty);
        }
    }
}
