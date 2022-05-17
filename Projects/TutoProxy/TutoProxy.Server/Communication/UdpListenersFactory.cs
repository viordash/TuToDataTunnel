using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    internal class UdpListenersFactory : IDisposable {
        readonly List<UdpListener> listeners;

        public UdpListenersFactory(string host, List<int> ports, IRequestProcessingService requestProcessingService, ILogger logger) {
            Guard.NotNull(ports, nameof(ports));
            Guard.NotNull(requestProcessingService, nameof(requestProcessingService));
            Guard.NotNull(logger, nameof(logger));

            var uri = new Uri(host);
            var ipAddresses = Dns.GetHostEntry(uri.Host).AddressList
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();
            var ipLocalEndPoint = new IPEndPoint(ipAddresses[0], 0);

            listeners = ports
                .Select(x => new UdpListener(x, ipLocalEndPoint, requestProcessingService, logger))
                .ToList();
        }

        public async void Listen(CancellationToken cancellationToken) {
            await Task.WhenAll(listeners.Select(x => x.Listen(cancellationToken)));
        }

        public void Dispose() {
            foreach(var item in listeners) {
                item.Dispose();
            }
        }

    }
}
