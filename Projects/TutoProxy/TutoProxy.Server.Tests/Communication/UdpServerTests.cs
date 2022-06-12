﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;


namespace TutoProxy.Server.Tests.Communication {
    public class UdpServerTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        class TestableUdpServer : UdpServer {
            public TestableUdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IDateTimeService dateTimeService)
                : base(port, localEndPoint, dataTransferService, logger, dateTimeService) {
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

        Mock<ILogger> loggerMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        Mock<IDateTimeService> dateTimeServiceMock;

        TimeSpan requestTimeout;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            dataTransferServiceMock = new();
            dateTimeServiceMock = new();

            dateTimeServiceMock
                .SetupGet(x => x.RequestTimeout)
                .Returns(() => requestTimeout);
        }

        [Test]
        [Retry(3)]
        public async Task RemoteEndPoints_Are_AutoDelete_After_Timeout_Test() {
            using var testable = new TestableUdpServer(0, new IPEndPoint(IPAddress.Loopback, 0), dataTransferServiceMock.Object, loggerMock.Object, dateTimeServiceMock.Object);

            requestTimeout = TimeSpan.FromMilliseconds(500);
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
            using var testable = new TestableUdpServer(0, new IPEndPoint(IPAddress.Loopback, 0), dataTransferServiceMock.Object, loggerMock.Object, dateTimeServiceMock.Object);

            requestTimeout = TimeSpan.FromMilliseconds(500);
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
            var testable = new TestableUdpServer(0, new IPEndPoint(IPAddress.Loopback, 0), dataTransferServiceMock.Object, loggerMock.Object, dateTimeServiceMock.Object);

            requestTimeout = TimeSpan.FromMilliseconds(500);
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
