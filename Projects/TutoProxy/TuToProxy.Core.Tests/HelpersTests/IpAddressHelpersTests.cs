using System;
using System.Net.Sockets;
using NUnit.Framework;
using TuToProxy.Core.Helpers;

namespace TuToProxy.Core.Tests.HelpersTests {
    public class IpAddressHelpersTests {

        [Test]
        public void ParseUrl_Test() {
            var ipAddress = IpAddressHelpers.ParseUrl("http://localhost");
            Assert.That(ipAddress.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork).Or.EqualTo(AddressFamily.InterNetworkV6));
        }

        [Test]
        public void ParseUrl_For_IP4_Return_InterNetwork_Test() {
            var ipAddress = IpAddressHelpers.ParseUrl("http://127.0.0.1");
            Assert.That(ipAddress.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
            Assert.That(ipAddress.GetAddressBytes(), Is.EquivalentTo(new byte[] { 127, 0, 0, 1 }));
        }

        [Test]
        public void ParseUrl_For_IP4_Local_Return_InterNetwork_Test() {
            var ipAddress = IpAddressHelpers.ParseUrl("http://0.0.0.1");
            Assert.That(ipAddress.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
            Assert.That(ipAddress.GetAddressBytes(), Is.EquivalentTo(new byte[] { 0, 0, 0, 1 }));
        }

        [Test]
        public void ParseUrl_For_IP6_Host_Return_InterNetworkV6_Test() {
            var ipAddress = IpAddressHelpers.ParseUrl("http://[::1]");
            Assert.That(ipAddress.AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
        }

        [Test]
        public void ParseUrl_Incorrect_Uri_Throws_UriFormatException() {
            Assert.Throws<UriFormatException>(() => IpAddressHelpers.ParseUrl("http://..1"));
        }

        [Test, Explicit]
        public void ParseUrl_For_Domain_Named_Return_InterNetwork_Test() {
            var ipAddress = IpAddressHelpers.ParseUrl("https://google.com");
            Assert.That(ipAddress.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
        }

    }
}
