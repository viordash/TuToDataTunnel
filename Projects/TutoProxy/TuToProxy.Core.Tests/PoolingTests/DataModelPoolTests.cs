using NUnit.Framework;
using TuToProxy.Core.Models;
using TuToProxy.Core.Pooling;

namespace TuToProxy.Core.Tests.PoolingTests {
    public class DataModelPoolTests {

        [Test]
        public void Rent_ReturnsNewInstance() {
            var model = DataModelPool<TcpDataRequestModel>.Rent();

            Assert.That(model, Is.Not.Null);
            Assert.That(model, Is.InstanceOf<TcpDataRequestModel>());
        }

        [Test]
        public void Rent_ReturnsInstanceWithDefaultValues() {
            var model = DataModelPool<TcpDataRequestModel>.Rent();

            Assert.That(model.Port, Is.EqualTo(0));
            Assert.That(model.OriginPort, Is.EqualTo(0));
            Assert.That(model.Data.IsEmpty, Is.True);
        }

        [Test]
        public void Return_ResetsModelValues() {
            var model = DataModelPool<TcpDataRequestModel>.Rent();
            model.Port = 8080;
            model.OriginPort = 12345;
            model.Data = new byte[] { 1, 2, 3 };

            DataModelPool<TcpDataRequestModel>.Return(model);

            Assert.That(model.Port, Is.EqualTo(0));
            Assert.That(model.OriginPort, Is.EqualTo(0));
            Assert.That(model.Data.IsEmpty, Is.True);
        }

        [Test]
        public void Return_ObjectIsReusedFromPool() {
            var model1 = DataModelPool<TcpDataRequestModel>.Rent();
            model1.Port = 1000;
            model1.OriginPort = 2000;
            model1.Data = new byte[] { 1, 2, 3 };

            DataModelPool<TcpDataRequestModel>.Return(model1);

            var model2 = DataModelPool<TcpDataRequestModel>.Rent();

            Assert.That(model2, Is.SameAs(model1));
            Assert.That(model2.Port, Is.EqualTo(0));
            Assert.That(model2.OriginPort, Is.EqualTo(0));
            Assert.That(model2.Data.IsEmpty, Is.True);
        }

        [Test]
        public void Pool_WorksWithAllModelTypes() {
            var tcpRequest = DataModelPool<TcpDataRequestModel>.Rent();
            var tcpResponse = DataModelPool<TcpDataResponseModel>.Rent();
            var udpRequest = DataModelPool<UdpDataRequestModel>.Rent();
            var udpResponse = DataModelPool<UdpDataResponseModel>.Rent();

            Assert.That(tcpRequest, Is.InstanceOf<TcpDataRequestModel>());
            Assert.That(tcpResponse, Is.InstanceOf<TcpDataResponseModel>());
            Assert.That(udpRequest, Is.InstanceOf<UdpDataRequestModel>());
            Assert.That(udpResponse, Is.InstanceOf<UdpDataResponseModel>());

            DataModelPool<TcpDataRequestModel>.Return(tcpRequest);
            DataModelPool<TcpDataResponseModel>.Return(tcpResponse);
            DataModelPool<UdpDataRequestModel>.Return(udpRequest);
            DataModelPool<UdpDataResponseModel>.Return(udpResponse);
        }

        [Test]
        public void Pool_EachTypeHasSeparatePool() {
            var tcpRequest = DataModelPool<TcpDataRequestModel>.Rent();
            var udpRequest = DataModelPool<UdpDataRequestModel>.Rent();

            DataModelPool<TcpDataRequestModel>.Return(tcpRequest);
            DataModelPool<UdpDataRequestModel>.Return(udpRequest);

            var tcpRequest2 = DataModelPool<TcpDataRequestModel>.Rent();
            var udpRequest2 = DataModelPool<UdpDataRequestModel>.Rent();

            Assert.That(tcpRequest2, Is.SameAs(tcpRequest));
            Assert.That(udpRequest2, Is.SameAs(udpRequest));
            Assert.That(tcpRequest2, Is.Not.SameAs(udpRequest2));
        }

        [Test]
        public void MultipleRent_WithoutReturn_CreatesNewInstances() {
            var model1 = DataModelPool<TcpDataRequestModel>.Rent();
            var model2 = DataModelPool<TcpDataRequestModel>.Rent();

            Assert.That(model1, Is.Not.SameAs(model2));

            DataModelPool<TcpDataRequestModel>.Return(model1);
            DataModelPool<TcpDataRequestModel>.Return(model2);
        }
    }
}
