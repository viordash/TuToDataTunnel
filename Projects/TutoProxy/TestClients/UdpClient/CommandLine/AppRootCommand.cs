using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Тестовый udp-клиент") {
            Add(new Argument<string>("ip", "Remote UDP IP address"));
            Add(new Argument<int>("port", "Remote UDP IP port"));
            var argDelay = new Argument<int>("delay", () => 1000, "Delay before repeat, ms. Min value is 0ms");
            Add(argDelay);
            var argPacketSize = new Argument<int>("packet", () => 1400, "Packet size, bytes. Min value is 1");
            Add(argPacketSize);
            Add(new Option<bool>("--firenforget", () => false, "Fire'n'Forget"));
            AddValidator((result) => {
                try {
                    if(result.Children.Any(x => x.GetValueForArgument(argDelay) < 0)) {
                        result.ErrorMessage = "Delay should be higher or equal than 0ms";
                        return;
                    }
                    if(result.Children.Any(x => x.GetValueForArgument(argPacketSize) < 1)) {
                        result.ErrorMessage = "The packet size must be greater than or equal to 1 byte.";
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
            public int Packet { get; set; }
            public bool Firenforget { get; set; }

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
                var remoteEndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);

                using var udpServer = new UdpClient();
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                udpServer.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                udpServer.ExclusiveAddressUse = false;
                udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                var localPort = (udpServer.Client.LocalEndPoint as IPEndPoint)!.Port;

                var sRateStopWatch = new Stopwatch();
                var logTimer = DateTime.Now.AddSeconds(1);
                double sRate = 0;
                int packetsCount = 0;

                while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                    var dataPacket = Enumerable.Repeat(Guid.NewGuid().ToByteArray(), (Packet / 16) + 1)
                        .SelectMany(x => x)
                        .Take(Packet).ToArray();
                    sRateStopWatch.Restart();
                    var txCount = await udpServer.SendAsync(dataPacket, remoteEndPoint, applicationLifetime.ApplicationStopping);

                    if(!Firenforget) {
                        try {
                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping);
                            cts.CancelAfter(TimeSpan.FromMilliseconds(5000));

                            var result = await udpServer.ReceiveAsync(applicationLifetime.ApplicationStopping);
                            sRateStopWatch.Stop();
                            if(dataPacket.SequenceEqual(result.Buffer)) {
                                var ts = sRateStopWatch.Elapsed;
                                sRate += result.Buffer.Length / ts.TotalMilliseconds;
                                packetsCount++;
                                if(logTimer <= DateTime.Now) {
                                    logTimer = DateTime.Now.AddSeconds(1);
                                    logger.Information($"udp({localPort}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}, packets:{packetsCount}, srate:{(sRate / packetsCount):0} KB/s. Success");
                                    sRate = 0;
                                    packetsCount = 0;
                                }
                            } else {
                                logger.Warning($"udp({localPort}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}. Wrong");
                            }
                        } catch(OperationCanceledException) {
                            logger.Warning($"udp({localPort}) response timeout");
                        }
                    } else {
                        logger.Information($"udp({localPort}) request to {remoteEndPoint}, bytes:{txCount}");
                        await Task.Delay(10);
                    }
                    if(Delay > 0) {
                        await Task.Delay(Delay);
                    }
                }

                _ = applicationLifetime.ApplicationStopping.WaitHandle.WaitOne();
                return 0;
            }
        }
    }
}
