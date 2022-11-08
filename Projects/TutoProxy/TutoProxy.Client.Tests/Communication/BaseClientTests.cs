using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Tests.Communication {
    public class BaseClientTests {

        public class TestableClient : BaseClient<Socket> {
            public bool Timeout = false;

            protected override TimeSpan ReceiveTimeout { get { return TimeSpan.FromMilliseconds(400); } }

            public TestableClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
                : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            }

            protected override void OnTimedEvent(object? state) {
                if(Port == 700 && OriginPort == 100) {
                    Timeout = true;
                }
            }

            protected override Socket CreateSocket() {
                return new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }

        }


        Mock<ILogger> loggerMock;
        Mock<IClientsService> clientsServiceMock;
        Mock<ISignalRClient> signalRClientMock;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            clientsServiceMock = new();
            signalRClientMock = new();
        }

        [Test]
        public async Task TimeoutAction_Test() {
            var testable = new TestableClient(new IPEndPoint(IPAddress.Any, 700), 100, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object);

            await Task.Delay(200);
            Assert.That(testable.Timeout, Is.False);

            await Task.Delay(300);
            Assert.That(testable.Timeout, Is.True);
        }

        [Test]
        public async Task Refresh_Shifts_Start_TimeoutAction_Test() {
            var testable = new TestableClient(new IPEndPoint(IPAddress.Any, 700), 100, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object);
            await Task.Delay(200);
            Assert.That(testable.Timeout, Is.False);
            testable.Refresh();
            await Task.Delay(300);
            Assert.That(testable.Timeout, Is.False);
            await Task.Delay(200);
            Assert.That(testable.Timeout, Is.True);
        }

        [Test]
        //[Repeat(2)]
        public async Task Refresh_Concurrent_Test() {
            var testable = new TestableClient(new IPEndPoint(IPAddress.Any, 700), 100, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(30000));

            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);
            int startedCount = 0;
            var tasks = Enumerable.Range(1, 100).Select(x => new Task(async () => {
                ewh.WaitOne();
                await Task.Delay(1);
                testable.Refresh();
                startedCount++;
            }, cts.Token))
                .ToList();

            _ = Parallel.ForEach(tasks, task => {
                task.Start();
            });
            await Task.Delay(100);
            ewh.Set();
            await Task.WhenAll(tasks);

            await Task.Delay(500);
            Assert.That(testable.Timeout, Is.True);
            Assert.That(startedCount, Is.EqualTo(100));
        }
    }
}
