using System.Net;
using System.Net.Sockets;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Tests.Communication {
    public class BaseClientTests {

        public class TestableClient : BaseClient {
            public TestableClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient,
                IProcessMonitor processMonitor)
                : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient, processMonitor) {
            }

            public CancellationTokenSource PublicMorozov_CancellationTokenSource => cancellationTokenSource;
        }

        Mock<ILogger> loggerMock;
        Mock<IClientsService> clientsServiceMock;
        Mock<ISignalRClient> signalRClientMock;
        Mock<IProcessMonitor> processMonitorMock;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            clientsServiceMock = new();
            signalRClientMock = new();
            processMonitorMock = new();
        }


        [Test]
        public async Task CancellationTokenSource_Canceled_On_Dispose_Test() {
            var testable = new TestableClient(new IPEndPoint(IPAddress.Loopback, 80), 8001, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object,
                    processMonitorMock.Object);

            Assert.That(testable.PublicMorozov_CancellationTokenSource.IsCancellationRequested, Is.False);

            await testable.DisposeAsync();
            Assert.That(testable.PublicMorozov_CancellationTokenSource.IsCancellationRequested, Is.True);
        }
    }
}
