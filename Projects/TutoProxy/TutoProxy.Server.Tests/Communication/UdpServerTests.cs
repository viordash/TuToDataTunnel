using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;


namespace TutoProxy.Server.Tests.Communication {
    public class UdpServerTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


        Mock<ILogger> loggerMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        Mock<IServiceProvider> serviceProviderMock;
        Mock<IClientProxy> clientProxyMock;

        class TestableUdpServer : UdpServer {
            public TestableUdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, TimeSpan receiveTimeout, IClientProxy clientProxy, IServiceProvider serviceProvider)
                : base(port, localEndPoint, dataTransferService, new HubClient(localEndPoint, clientProxy, Enumerable.Range(1, 10).ToList(), Enumerable.Range(1000, 4), serviceProvider), logger, receiveTimeout) {
            }

            public void PublicMorozovAddRemoteEndPoint(IPEndPoint endPoint) {
                AddRemoteEndPoint(endPoint);
            }


            public ConcurrentDictionary<int, RemoteEndPoint> PublicMorozovRemoteEndPoints {
                get { return remoteEndPoints; }
            }

            public override void Dispose() {
                base.Dispose();
            }
        }

        [SetUp]
        public void Setup() {
            loggerMock = new();
            dataTransferServiceMock = new();
            serviceProviderMock = new();
            clientProxyMock = new();

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
        [Retry(3)]
        public async Task RemoteEndPoints_Are_AutoDelete_After_Timeout_Test() {
            using var testable = new TestableUdpServer(0, new IPEndPoint(IPAddress.Loopback, 0), dataTransferServiceMock.Object, loggerMock.Object,
                TimeSpan.FromMilliseconds(500), clientProxyMock.Object, serviceProviderMock.Object);

            testable.PublicMorozovAddRemoteEndPoint(new IPEndPoint(IPAddress.Loopback, 100));
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));

            await Task.Delay(200);

            testable.PublicMorozovAddRemoteEndPoint(new IPEndPoint(IPAddress.Loopback, 101));
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100, 101 }));

            await Task.Delay(310);
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 101 }));

            await Task.Delay(200);
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.Empty);
        }

        [Test]
        [Retry(3)]
        public async Task Add_Already_Exists_RemoteEndPoint_Increase_Timeout_Test() {
            using var testable = new TestableUdpServer(0, new IPEndPoint(IPAddress.Loopback, 0), dataTransferServiceMock.Object, loggerMock.Object,
                TimeSpan.FromMilliseconds(500), clientProxyMock.Object, serviceProviderMock.Object);

            testable.PublicMorozovAddRemoteEndPoint(new IPEndPoint(IPAddress.Loopback, 100));
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));

            await Task.Delay(200);

            testable.PublicMorozovAddRemoteEndPoint(new IPEndPoint(IPAddress.Loopback, 100));
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));

            await Task.Delay(410);
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));
            testable.PublicMorozovAddRemoteEndPoint(new IPEndPoint(IPAddress.Loopback, 100));

            await Task.Delay(410);
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));

            await Task.Delay(100);
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.Empty);
        }

        [Test]
        [Retry(3)]
        public async Task OnDispose_The_RemoteEndPoint_Timer_Cancelling_Test() {
            var testable = new TestableUdpServer(0, new IPEndPoint(IPAddress.Loopback, 0), dataTransferServiceMock.Object, loggerMock.Object,
                TimeSpan.FromMilliseconds(500), clientProxyMock.Object, serviceProviderMock.Object);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            testable.PublicMorozovAddRemoteEndPoint(new IPEndPoint(IPAddress.Loopback, 100));
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));
            await Task.Delay(100);
            testable.Dispose();
            await Task.Delay(500);
            Assert.That(testable.PublicMorozovRemoteEndPoints.Keys, Is.EquivalentTo(new[] { 100 }));
            stopWatch.Stop();

            Assert.That(stopWatch.Elapsed, Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(1000)));
        }

    }
}
