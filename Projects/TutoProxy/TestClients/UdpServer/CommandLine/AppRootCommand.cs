using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Тестовый udp-сервер") {
            Add(new Argument<int>("port", "Listen UDP IP port"));
            var argDelay = new Argument<int>("delay", () => 10, "Delay before response, ms. Min value is 0ms");
            Add(argDelay);
            AddValidator((result) => {
                try {
                    if(result.Children.Any(x => x.GetValueForArgument(argDelay) < 0)) {
                        result.ErrorMessage = "Delay should be higher or equal than 0ms";
                        return;
                    }
                } catch(InvalidOperationException) {
                    result.ErrorMessage = "not valid";
                }
            });
        }

        public new class Handler : ICommandHandler {
            readonly ILogger logger;
            readonly IHostApplicationLifetime applicationLifetime;

            public int Port { get; set; }
            public int Delay { get; set; }

            public Handler(
                ILogger logger,
                IHostApplicationLifetime applicationLifetime
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
                this.logger = logger;
                this.applicationLifetime = applicationLifetime;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                using var udpServer = new UdpClient(Port);
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                udpServer.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                var localPort = (udpServer.Client.LocalEndPoint as IPEndPoint)!.Port;

                while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                    var result = await udpServer.ReceiveAsync(applicationLifetime.ApplicationStopping);
                    logger.Information($"udp({localPort}) request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                    await Task.Delay(Delay);
                    var txCount = await udpServer.SendAsync(result.Buffer, result.RemoteEndPoint, applicationLifetime.ApplicationStopping);
                }

                _ = applicationLifetime.ApplicationStopping.WaitHandle.WaitOne();
                return 0;
            }
        }
    }
}
