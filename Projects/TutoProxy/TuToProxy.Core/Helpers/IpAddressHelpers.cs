using System.Net;

namespace TuToProxy.Core.Helpers {
    public class IpAddressHelpers {
        public static IPAddress ParseUrl(string url) {
            var uri = new Uri(url);
            try {
                return IPAddress.Parse(uri.Host);
            } catch(FormatException) {
                return Dns.GetHostEntry(uri.DnsSafeHost).AddressList[0];
            }
        }
    }
}
