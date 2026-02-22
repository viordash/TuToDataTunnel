using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        const string description = "Тестовый udp-клиент";

        public Argument<string> IpArg { get; }
        public Argument<int> PortArg { get; }
        public Argument<int> DelayArg { get; }
        public Argument<int> PacketArg { get; }
        public Option<bool> FirenforgetOpt { get; }

        public AppRootCommand() : base(description) {
            IpArg = new Argument<string>("ip") { Description = "Remote UDP IP address" };
            PortArg = new Argument<int>("port") { Description = "Remote UDP IP port" };
            DelayArg = new Argument<int>("delay") { Description = "Delay before repeat, ms. Min value is 0ms", DefaultValueFactory = _ => 1000 };
            PacketArg = new Argument<int>("packet") { Description = "Packet size, bytes. Min value is 1", DefaultValueFactory = _ => 1400 };
            FirenforgetOpt = new Option<bool>("--firenforget") { Description = "Fire'n'Forget", DefaultValueFactory = _ => false };

            Add(IpArg);
            Add(PortArg);
            Add(DelayArg);
            Add(PacketArg);
            Add(FirenforgetOpt);

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
                var firenforget = parseResult.GetValue(FirenforgetOpt);

                var serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"{description}, ip: {ip}, порт: {port}, delay: {delay}");

                while(!applicationStopping.IsCancellationRequested) {
                    using var udpClient = new UdpClient(serverEndPoint.AddressFamily);
                    uint IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                    udpClient.ExclusiveAddressUse = false;
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.Client.SendTimeout = 5000;
                    udpClient.Client.ReceiveTimeout = 30000;

                    var sRateStopWatch = new Stopwatch();
                    var logTimer = DateTime.Now.AddSeconds(1);
                    double sRate = 0;
                    int packetsCount = 0;
                    int errors = 0;

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
                    while(!cts.IsCancellationRequested) {
                        var dataPacket = Enumerable.Repeat(Guid.NewGuid().ToByteArray(), (packet / 16) + 1)
                            .SelectMany(x => x)
                            .Take(packet).ToArray();
                        sRateStopWatch.Restart();
                        var txCount = await udpClient.SendAsync(dataPacket, serverEndPoint, cts.Token);

                        var localPort = (udpClient.Client.LocalEndPoint as IPEndPoint)!.Port;

                        if(!firenforget) {
                            try {
                                cts.CancelAfter(TimeSpan.FromMilliseconds(30000));

                                IPEndPoint? remoteEndPoint;
                                var receiveBufferList = new List<byte[]>();
                                do {
                                    var result = await udpClient.ReceiveAsync(cts.Token);
                                    remoteEndPoint = result.RemoteEndPoint;
                                    receiveBufferList.Add(result.Buffer);
                                } while(receiveBufferList.Select(x => x.Length).Sum() != dataPacket.Length && !cts.Token.IsCancellationRequested);

                                var receiveBuffer = receiveBufferList.SelectMany(x => x).ToArray();
                                sRateStopWatch.Stop();
                                if(dataPacket.SequenceEqual(receiveBuffer)) {
                                    var ts = sRateStopWatch.Elapsed;
                                    sRate += receiveBuffer.Length / ts.TotalMilliseconds;
                                    packetsCount++;
                                    if(logTimer <= DateTime.Now) {
                                        logTimer = DateTime.Now.AddSeconds(1);
                                        logger.Information($"udp({localPort}) response from {remoteEndPoint}, bytes:{receiveBuffer.ToShortDescriptions()}, packets:{packetsCount}, srate:{(sRate / packetsCount):0} KB/s. Success");
                                        sRate = 0;
                                        packetsCount = 0;
                                    }
                                    errors = 0;
                                } else {
                                    logger.Warning($"udp({localPort}) response from {remoteEndPoint}, bytes:{receiveBuffer.ToShortDescriptions()}. Wrong");
                                    await Task.Delay(TimeSpan.FromMilliseconds(2000), applicationStopping);
                                    if(errors++ > 3) {
                                        errors = 0;
                                        break;
                                    }
                                }
                            } catch(OperationCanceledException) {
                                logger.Warning($"udp({localPort}) response timeout");
                            }
                        } else {
                            logger.Information($"udp({localPort}) request to {serverEndPoint}, bytes:{dataPacket.ToShortDescriptions()}");
                            await Task.Delay(10);
                        }
                        if(delay > 0) {
                            await Task.Delay(delay);
                        }
                    }
                }

                return 0;
            });
        }
    }
}
