using System.Collections.Concurrent;
using System.Net;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Tests.Services {
    public class UdpClientsServiceTests {

        public class TestableUdpClient : UdpClient {
            public TestableUdpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient,
                    IProcessMonitor processMonitor)
                : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient, processMonitor) {
            }

            protected override TimeSpan ReceiveTimeout { get { return TimeSpan.FromMilliseconds(1000); } }
        }

        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger, IClientFactory clientFactory, IProcessMonitor processMonitor)
                : base(logger, clientFactory, processMonitor) {
            }

            public ConcurrentDictionary<int, ConcurrentDictionary<int, UdpClient>> PublicMorozovUdpClients {
                get { return udpClients; }
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
                .Setup(x => x.CreateUdp(It.IsAny<IPAddress>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IClientsService>(), It.IsAny<ISignalRClient>(), It.IsAny<IProcessMonitor>()))
                .Returns<IPAddress, int, int, IClientsService, ISignalRClient, IProcessMonitor>((localIpAddress, port, originPort, clientsService, dataTunnelClient, processMonitor) => {
                    return new TestableUdpClient(new IPEndPoint(localIpAddress, port), originPort, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object,
                        processMonitorMock.Object);
                });

            testable = new TestableClientsService(loggerMock.Object, clientFactoryMock.Object, processMonitorMock.Object);
        }

        [Test]
        public void ObtainUdpClient_With_Banned_UdpPort_Are_Throws_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            Assert.Throws<ClientNotFoundException>(() => testable.ObtainUdpClient(999, 50999, signalRClientMock.Object));
            Assert.Throws<ClientNotFoundException>(() => testable.ObtainUdpClient(1005, 51005, signalRClientMock.Object));
        }

        [Test]
        public void ObtainUdpClient_Only_Once_Creating_List_With_Same_Port_Clients_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainUdpClient(1000, 51000, signalRClientMock.Object);
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51000], Is.SameAs(client0));

            var client1 = testable.ObtainUdpClient(1000, 51001, signalRClientMock.Object);
            Assert.IsNotNull(client1);
            Assert.That(client1.Port, Is.EqualTo(1000));
            Assert.That(client1.OriginPort, Is.EqualTo(51001));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000, 51001 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51001], Is.SameAs(client1));
        }

        [Test]
        public void ObtainUdpClient_Only_Once_Creating_Same_Port_Client_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainUdpClient(1000, 51000, signalRClientMock.Object);
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            var client1 = testable.ObtainUdpClient(1000, 51000, signalRClientMock.Object);
            Assert.That(client1, Is.SameAs(client0));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51000], Is.SameAs(client0));
        }

        [Test]
        public async Task UdpClients_Is_Auto_Removed_After_Timeout_Test() {
            testable.Start(IPAddress.Any, null, Enumerable.Range(1000, 50).ToList());

            for(int port = 0; port < 50; port++) {
                for(int origPort = 0; origPort < 10; origPort++) {
                    Assert.IsNotNull(testable.ObtainUdpClient(1000 + port, 51000 + origPort, signalRClientMock.Object));
                }
            }
            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Never);

            await Task.Delay(1100);

            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(500));
        }

        [Test]
        public async Task UdpClient_Timeout_Timer_Is_Refreshed_During_Obtaining_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 1).ToList());

            Assert.IsNotNull(testable.ObtainUdpClient(1000, 51000, signalRClientMock.Object));

            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Never);

            await Task.Delay(500);
            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Never);

            Assert.IsNotNull(testable.ObtainUdpClient(1000, 51000, signalRClientMock.Object));
            await Task.Delay(500);
            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Never);

            Assert.IsNotNull(testable.ObtainUdpClient(1000, 51000, signalRClientMock.Object));
            await Task.Delay(500);

            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Never);

            await Task.Delay(600);
            clientsServiceMock.Verify(x => x.RemoveUdpClient(It.IsAny<int>(), It.IsAny<int>()), Times.Once);


        }
    }
}
