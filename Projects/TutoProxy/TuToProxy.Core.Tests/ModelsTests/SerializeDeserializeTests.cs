using MessagePack;
using MessagePack.Resolvers;
using NUnit.Framework;
using TuToProxy.Core.Models;

namespace TuToProxy.Core.Tests.ModelsTests {
    public class SerializeDeserializeTests {
        MessagePackSerializerOptions messagePackSerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(AttributeFormatterResolver.Instance)
                .WithSecurity(MessagePackSecurity.UntrustedData);

        [SetUp]
        public void Setup() {
        }

        [Test]
        public void SocketAddressModel_Test() {
            var socketAddress = new SocketAddressModel() {
                Port = 1000, OriginPort = 2000
            };
            var bin = MessagePackSerializer.Serialize(socketAddress, messagePackSerializerOptions);

            var deserialized = MessagePackSerializer.Deserialize<SocketAddressModel>(bin, messagePackSerializerOptions);
            Assert.That(deserialized.Port, Is.EqualTo(socketAddress.Port));
            Assert.That(deserialized.OriginPort, Is.EqualTo(socketAddress.OriginPort));
        }

        [Test]
        public void TcpDataRequestModel_Test() {
            var data = new TcpDataRequestModel() {
                Port = 10000, OriginPort = 20000, Data = new byte[] { 0, 1, 2 }
            };
            var bin = MessagePackSerializer.Serialize(data, messagePackSerializerOptions);

            var deserialized = MessagePackSerializer.Deserialize<TcpDataRequestModel>(bin, messagePackSerializerOptions);
            Assert.That(deserialized.Port, Is.EqualTo(data.Port));
            Assert.That(deserialized.OriginPort, Is.EqualTo(data.OriginPort));
            Assert.That(deserialized.Data.ToArray(), Is.EquivalentTo(data.Data.ToArray()));
        }

        [Test]
        public void UdpDataRequestModel_Test() {
            var data = new UdpDataRequestModel() {
                Port = 10001, OriginPort = 20001, Data = new byte[] { 0, 1, 2, 4 }
            };
            var bin = MessagePackSerializer.Serialize(data, messagePackSerializerOptions);

            var deserialized = MessagePackSerializer.Deserialize<UdpDataRequestModel>(bin, messagePackSerializerOptions);
            Assert.That(deserialized.Port, Is.EqualTo(data.Port));
            Assert.That(deserialized.OriginPort, Is.EqualTo(data.OriginPort));
            Assert.That(deserialized.Data.ToArray(), Is.EquivalentTo(data.Data.ToArray()));
        }

        [Test]
        public void TcpDataResponseModel_Test() {
            var data = new TcpDataResponseModel() {
                Port = 10002, OriginPort = 20002, Data = new byte[] { 0, 1, 2, 4, 5 }
            };
            var bin = MessagePackSerializer.Serialize(data, messagePackSerializerOptions);

            var deserialized = MessagePackSerializer.Deserialize<TcpDataResponseModel>(bin, messagePackSerializerOptions);
            Assert.That(deserialized.Port, Is.EqualTo(data.Port));
            Assert.That(deserialized.OriginPort, Is.EqualTo(data.OriginPort));
            Assert.That(deserialized.Data.ToArray(), Is.EquivalentTo(data.Data.ToArray()));
        }

        [Test]
        public void UdpDataResponseModel_Test() {
            var data = new TcpDataResponseModel() {
                Port = 10003, OriginPort = 20003, Data = new byte[] { 0, 1, 2, 4, 5, 6 }
            };
            var bin = MessagePackSerializer.Serialize(data, messagePackSerializerOptions);

            var deserialized = MessagePackSerializer.Deserialize<TcpDataResponseModel>(bin, messagePackSerializerOptions);
            Assert.That(deserialized.Port, Is.EqualTo(data.Port));
            Assert.That(deserialized.OriginPort, Is.EqualTo(data.OriginPort));
            Assert.That(deserialized.Data.ToArray(), Is.EquivalentTo(data.Data.ToArray()));
        }

    }
}
