using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        const string description = "Тестовый udp-сервер";
        public AppRootCommand() : base(description) {
            Add(new Argument<string>("ip", "Listen UDP IP address"));
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

            public string Ip { get; set; } = string.Empty;
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
                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"{description}, ip: {Ip}, порт: {Port}, delay: {Delay}");

                while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                    using var udpServer = new UdpClient(new IPEndPoint(IPAddress.Parse(Ip), Port));
                    uint IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    udpServer.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                    udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    try {
                        var logTimer = DateTime.Now.AddSeconds(1);
                        while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                            var result = await udpServer.ReceiveAsync(applicationLifetime.ApplicationStopping);
                            if(logTimer <= DateTime.Now) {
                                logTimer = DateTime.Now.AddSeconds(1);
                                logger.Information($"udp({Port}) request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                            }
                            if(Delay > 0) {
                                await Task.Delay(Delay);
                            }
                            var txCount = await udpServer.SendAsync(result.Buffer, result.RemoteEndPoint, applicationLifetime.ApplicationStopping);
                        }
                    } catch(SocketException ex) {
                        logger.Error(ex.Message);
                    }
                }

                return 0;
            }
        }
    }
}
