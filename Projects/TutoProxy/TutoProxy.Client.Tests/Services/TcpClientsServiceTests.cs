using System.Collections.Concurrent;
using System.Net;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TuToProxy.Core.Models;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Tests.Services {
    public class TcpClientsServiceTests {

        public class TestableTcpClient : TcpClient {

            public TestableTcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
                : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            }

            protected override System.Net.Sockets.Socket CreateSocket() {
                return new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            }
        }

        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger, IClientFactory clientFactory)
                : base(logger, clientFactory) {
            }

            public ConcurrentDictionary<int, ConcurrentDictionary<int, TcpClient>> PublicMorozovTcpClients {
                get { return tcpClients; }
            }
        }

        Mock<ILogger> loggerMock;
        Mock<IClientFactory> clientFactoryMock;
        Mock<IClientsService> clientsServiceMock;
        Mock<ISignalRClient> signalRClientMock;

        TestableClientsService testable;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            clientFactoryMock = new();
            clientsServiceMock = new();
            signalRClientMock = new();

            clientFactoryMock
                .Setup(x => x.CreateTcp(It.IsAny<IPAddress>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IClientsService>(), It.IsAny<ISignalRClient>()))
                .Returns<IPAddress, int, int, IClientsService, ISignalRClient>((localIpAddress, port, originPort, clientsService, dataTunnelClient) => {
                    return new TestableTcpClient(new IPEndPoint(localIpAddress, port), originPort, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object);
                });

            testable = new TestableClientsService(loggerMock.Object, clientFactoryMock.Object);
        }

        [Test]
        public void ObtainTcpClient_With_Banned_TcpPort_Are_Throws_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1000, 4).ToList(), Enumerable.Range(1, 65535).ToList());

            Assert.Throws<ClientNotFoundException>(() => testable.ObtainTcpClient(999, 50999, signalRClientMock.Object));
            Assert.Throws<ClientNotFoundException>(() => testable.ObtainTcpClient(1005, 51005, signalRClientMock.Object));
        }

        [Test]
        public void ObtainTcpClient_Only_Once_Creating_List_With_Same_Port_Clients_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainTcpClient(1000, 51000, signalRClientMock.Object);
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000][51000], Is.SameAs(client0));

            var client1 = testable.ObtainTcpClient(1000, 51001, signalRClientMock.Object);
            Assert.IsNotNull(client1);
            Assert.That(client1.Port, Is.EqualTo(1000));
            Assert.That(client1.OriginPort, Is.EqualTo(51001));

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000, 51001 }));
            Assert.That(testable.PublicMorozovTcpClients[1000][51001], Is.SameAs(client1));
        }

        [Test]
        public void ObtainTcpClient_Only_Once_Creating_Same_Port_Client_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainTcpClient(1000, 51000, signalRClientMock.Object);
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            var client1 = testable.ObtainTcpClient(1000, 51000, signalRClientMock.Object);
            Assert.That(client1, Is.SameAs(client0));

            Assert.That(testable.PublicMorozovTcpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovTcpClients[1000][51000], Is.SameAs(client0));
        }
    }
}
