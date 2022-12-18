using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;

namespace TutoProxy.Server.Tests.Services {
    public class HubClientTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        Mock<IServiceProvider> serviceProviderMock;
        Mock<IClientProxy> clientProxyMock;
        Mock<IServerFactory> serverFactoryMock;
        Mock<ITcpServer> tcpServerMock;
        Mock<IUdpServer> udpServerMock;

        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        string? clientsRequest;

        [SetUp]
        public void Setup() {
            serviceProviderMock = new();
            clientProxyMock = new();
            serverFactoryMock = new();
            tcpServerMock = new();
            udpServerMock = new();

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
        public async Task SendUdpResponse_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            await testable.SendUdpResponse(new UdpDataResponseModel() { Port = 1000, OriginPort = 1000, Data = new byte[] { 0, 1 } });
            udpServerMock.Verify(x => x.SendResponse(It.IsAny<UdpDataResponseModel>()), Times.Once);
        }

        [Test]
        public void SendUdpResponse_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.ThrowsAsync<SocketPortNotBoundException>(async () => await testable.SendUdpResponse(
                new UdpDataResponseModel() { Port = 11, OriginPort = 10000, Data = new byte[] { 0, 1 } }),
                    "Udp socket port(11) not bound");
        }

        [Test]
        public void DisconnectUdp_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            testable.DisconnectUdp(new SocketAddressModel() { Port = 1000, OriginPort = 10000 }, 42);
            udpServerMock.Verify(x => x.Disconnect(It.IsAny<SocketAddressModel>(), It.IsAny<long>()), Times.Once);
        }

        [Test]
        public void DisconnectUdp_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(), Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.Throws<SocketPortNotBoundException>(() => testable.DisconnectUdp(new SocketAddressModel() { Port = 11, OriginPort = 10000 }, 42),
                    "Udp socket port(11) not bound");
        }

        [Test]
        public async Task SendTcpResponse_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            await testable.SendTcpResponse(new TcpDataResponseModel() { Port = 10, OriginPort = 1000, Data = new byte[] { 0, 1 } });
            tcpServerMock.Verify(x => x.SendResponse(It.IsAny<TcpDataResponseModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void SendTcpResponse_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.ThrowsAsync<SocketPortNotBoundException>(async () => await testable.SendTcpResponse(
                new TcpDataResponseModel() { Port = 110, OriginPort = 10000, Data = new byte[] { 0, 1 } }),
                    "Tcp socket port(110) not bound");
        }

        [Test]
        public void DisconnectTcp_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            testable.DisconnectTcp(new SocketAddressModel() { Port = 10, OriginPort = 10000 });
            tcpServerMock.Verify(x => x.DisconnectAsync(It.IsAny<SocketAddressModel>()), Times.Once);
        }

        [Test]
        public void DisconnectTcp_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(),
                Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.Throws<SocketPortNotBoundException>(() => testable.DisconnectTcp(new SocketAddressModel() { Port = 110, OriginPort = 10000 }),
                    "Tcp socket port(110) not bound");
        }
    }
}
