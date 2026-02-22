using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        const string description = "Тестовый tcp-клиент";

        public Argument<string> IpArg { get; }
        public Argument<int> PortArg { get; }
        public Argument<int> DelayArg { get; }
        public Argument<int> PacketArg { get; }

        public AppRootCommand() : base(description) {
            IpArg = new Argument<string>("ip") { Description = "Remote TCP IP address" };
            PortArg = new Argument<int>("port") { Description = "Remote TCP IP port" };
            DelayArg = new Argument<int>("delay") { Description = "Delay before repeat, ms. Min value is 0ms", DefaultValueFactory = _ => 1000 };
            PacketArg = new Argument<int>("packet") { Description = "Packet size, bytes. Min value is 1", DefaultValueFactory = _ => 1400 };

            Add(IpArg);
            Add(PortArg);
            Add(DelayArg);
            Add(PacketArg);

            Validators.Add((result) => {
                try {
                    if(result.Children.Any(x => x.GetValue(DelayArg) < 0)) {
                        result.AddError("Delay should be higher or equal than 0ms");
                        return;
                    }
                    if(result.Children.Any(x => x.GetValue(PacketArg) < 1)) {
                        result.AddError("The packet size must be greater than or equal to 1 byte.");
                        return;
                    }
                } catch(InvalidOperationException) {
                    result.AddError("not valid");
                }
            });
        }

        public void ConfigureAction(Serilog.ILogger logger, CancellationToken applicationStopping) {
            SetAction(async (parseResult, cancellationToken) => {
                var ip = parseResult.GetValue(IpArg)!;
                var port = parseResult.GetValue(PortArg);
                var delay = parseResult.GetValue(DelayArg);
                var packet = parseResult.GetValue(PacketArg);

                var remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"{description}, ip: {ip}, порт: {port}, delay: {delay}");

                while(!applicationStopping.IsCancellationRequested) {
                    using(var tcpClient = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)) {
                        int localPort = -1;
                        try {
                            await tcpClient.ConnectAsync(remoteEndPoint, applicationStopping);
                            localPort = (tcpClient.LocalEndPoint as IPEndPoint)!.Port;
                            logger.Information($"tcp({localPort}) success connected");

                        } catch(SocketException) {
                            logger.Warning($"tcp({localPort}) connect to {remoteEndPoint} timeout");
                            await Task.Delay(5_000, applicationStopping);
                            continue;
                        }

                        var sRateStopWatch = new Stopwatch();
                        var logTimer = DateTime.Now.AddSeconds(1);
                        double sRate = 0;
                        int packetsCount = 0;
                        int errors = 0;

                        Memory<byte> receiveBuffer = new byte[Math.Max(16384, packet)];

                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
                        while(!applicationStopping.IsCancellationRequested && tcpClient.Connected) {
                            var dataPacket = Enumerable.Repeat(Guid.NewGuid().ToByteArray(), (packet / 16) + 1)
                                .SelectMany(x => x)
                                .Take(packet).ToArray();
                            sRateStopWatch.Restart();
                            try {
                                var txCount = await tcpClient.SendAsync(dataPacket, SocketFlags.None, cts.Token);
                                cts.CancelAfter(TimeSpan.FromMilliseconds(30000));

                                int totalBytes = 0;
                                do {
                                    var receivedBytes = await tcpClient.ReceiveAsync(receiveBuffer.Slice(totalBytes), SocketFlags.None, cts.Token);
                                    totalBytes += receivedBytes;
                                } while(totalBytes < dataPacket.Length && !cts.Token.IsCancellationRequested);
                                sRateStopWatch.Stop();
                                var data = receiveBuffer[..totalBytes].ToArray();
                                if(dataPacket.SequenceEqual(data)) {
                                    var ts = sRateStopWatch.Elapsed;
                                    sRate += totalBytes / ts.TotalMilliseconds;
                                    packetsCount++;
                                    if(logTimer <= DateTime.Now) {
                                        logTimer = DateTime.Now.AddSeconds(1);
                                        logger.Information($"tcp({localPort}) response from {tcpClient.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}, packets:{packetsCount}, srate:{(sRate / packetsCount):0} KB/s. Success");
                                        sRate = 0;
                                        packetsCount = 0;
                                    }
                                    errors = 0;
                                } else {
                                    logger.Warning($"tcp({localPort}) response from {tcpClient.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}. Wrong");
                                    await Task.Delay(TimeSpan.FromMilliseconds(2000), applicationStopping);
                                    if(errors++ > 3) {
                                        break;
                                    }
                                }
                            } catch(Exception ex) when(ex is SocketException || ex is OperationCanceledException) {
                                logger.Warning($"tcp({localPort}) response error");
                                await Task.Delay(TimeSpan.FromMilliseconds(1000), applicationStopping);
                                if(errors++ > 3) {
                                    break;
                                }
                            }

                            if(delay > 0) {
                                await Task.Delay(delay, applicationStopping);
                            }
                        }
                        logger.Warning($"tcp({localPort}) disconnecting");
                    }
                }

                return 0;
            });
        }
    }
}
