using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Tests.Communication {
    public class BaseClientTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public class TestableClient : BaseClient {
            public TestableClient(BaseServer server, int originPort, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor)
                : base(server, originPort, dataTransferService, logger, processMonitor) {
            }

            public CancellationTokenSource PublicMorozov_CancellationTokenSource => cancellationTokenSource;
        }

        public class TestServer : BaseServer {
            public TestServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor)
                : base(port, localEndPoint, dataTransferService, logger, processMonitor) {
            }

            public override void Dispose() {
                throw new NotImplementedException();
            }
        }

        Mock<ILogger> loggerMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        Mock<IProcessMonitor> processMonitorMock;
        TestServer testServer;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            dataTransferServiceMock = new();
            processMonitorMock = new();
            testServer = new TestServer(1000, new IPEndPoint(IPAddress.Loopback, 80), dataTransferServiceMock.Object, loggerMock.Object, processMonitorMock.Object);
        }

        [Test]
        public async Task CancellationTokenSource_Disposing_Test() {
            var testable = new TestableClient(testServer, 8001, dataTransferServiceMock.Object, loggerMock.Object, processMonitorMock.Object);

            Assert.That(testable.PublicMorozov_CancellationTokenSource.IsCancellationRequested, Is.False);

            await testable.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => { var token = testable.PublicMorozov_CancellationTokenSource.Token; });
            Assert.That(testable.PublicMorozov_CancellationTokenSource.IsCancellationRequested, Is.False);
        }
    }
}
