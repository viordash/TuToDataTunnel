using System;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using TuToProxy.Core.Models;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Tests.Services {
    public class HubClientTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        Mock<ILogger> loggerMock;
        Mock<IServiceProvider> serviceProviderMock;
        Mock<IClientProxy> clientProxyMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        Mock<IProcessMonitor> processMonitorMock;
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        string? clientsRequest;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            serviceProviderMock = new();
            clientProxyMock = new();
            dataTransferServiceMock = new();
            processMonitorMock = new();

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
                        _ when type == typeof(IProcessMonitor) => processMonitorMock.Object,
                        _ => null
                    };
                });

        }

        [Test]
        public void SendUdpResponse_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(), Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.ThrowsAsync<SocketPortNotBoundException>(async () => await testable.SendUdpResponse(
                new UdpDataResponseModel() { Port = 11, OriginPort = 10000, Data = new byte[] { 0, 1 } }),
                    "Udp socket port(11) not bound");
        }
    }
}
