using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Тестовый tcp-клиент") {
            Add(new Argument<string>("ip", "Remote TCP IP address"));
            Add(new Argument<int>("port", "Remote TCP IP port"));
            var argDelay = new Argument<int>("delay", () => 1000, "Delay before repeat, ms. Min value is 0ms");
            Add(argDelay);
            var argPacketSize = new Argument<int>("packet", () => 1400, "Packet size, bytes. Min value is 1");
            Add(argPacketSize);
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

                while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                    using(var tcpClient = new Socket(SocketType.Stream, ProtocolType.Tcp)) {
                        try {
                            await tcpClient.ConnectAsync(remoteEndPoint, applicationLifetime.ApplicationStopping);
                        } catch(SocketException) {
                            logger.Warning($"tcp({Port}) connect timeout");
                            await Task.Delay(5_000, applicationLifetime.ApplicationStopping);
                            continue;
                        }
                        var localPort = (tcpClient.LocalEndPoint as IPEndPoint)!.Port;

                        var sRateStopWatch = new Stopwatch();
                        var logTimer = DateTime.Now.AddSeconds(1);
                        double sRate = 0;
                        int packetsCount = 0;
                        int errors = 0;

                        while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                            var dataPacket = Enumerable.Repeat(Guid.NewGuid().ToByteArray(), (Packet / 16) + 1)
                                .SelectMany(x => x)
                                .Take(Packet).ToArray();
                            sRateStopWatch.Restart();
                            var txCount = await tcpClient.SendAsync(dataPacket, SocketFlags.None, applicationLifetime.ApplicationStopping);

                            try {
                                using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping);
                                cts.CancelAfter(TimeSpan.FromMilliseconds(5000));

                                Memory<byte> receiveBuffer = new byte[Math.Max(16384, Packet)];
                                var receivedBytes = await tcpClient.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);

                                sRateStopWatch.Stop();
                                if(dataPacket.SequenceEqual(receiveBuffer[..receivedBytes].ToArray())) {
                                    var ts = sRateStopWatch.Elapsed;
                                    sRate += receivedBytes / ts.TotalMilliseconds;
                                    packetsCount++;
                                    if(logTimer <= DateTime.Now) {
                                        logTimer = DateTime.Now.AddSeconds(1);
                                        logger.Information($"tcp({localPort}) response from {tcpClient.RemoteEndPoint}, bytes:{receivedBytes}, packets:{packetsCount}, srate:{(sRate / packetsCount):0} KB/s. Success");
                                        sRate = 0;
                                        packetsCount = 0;
                                    }
                                    errors = 0;
                                } else {
                                    logger.Warning($"tcp({localPort}) response from {tcpClient.RemoteEndPoint}, bytes:{receivedBytes}. Wrong");
                                    await Task.Delay(TimeSpan.FromMilliseconds(2000), applicationLifetime.ApplicationStopping);
                                    if(errors++ > 3) {
                                        break;
                                    }
                                }
                            } catch(OperationCanceledException) {
                                logger.Warning($"tcp({localPort}) response timeout");
                            }

                            if(Delay > 0) {
                                await Task.Delay(Delay, applicationLifetime.ApplicationStopping);
                            }
                        }
                    }
                }

                return 0;
            }
        }
    }
}
