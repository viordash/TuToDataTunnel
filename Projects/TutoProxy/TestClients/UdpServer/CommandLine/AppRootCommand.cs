using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        const string description = "Тестовый udp-сервер";

        public Argument<string> IpArg { get; }
        public Argument<int> PortArg { get; }
        public Argument<int> DelayArg { get; }

        public AppRootCommand() : base(description) {
            IpArg = new Argument<string>("ip") { Description = "Listen UDP IP address" };
            PortArg = new Argument<int>("port") { Description = "Listen UDP IP port" };
            DelayArg = new Argument<int>("delay") { Description = "Delay before response, ms. Min value is 0ms", DefaultValueFactory = _ => 10 };

            Add(IpArg);
            Add(PortArg);
            Add(DelayArg);

            Validators.Add((result) => {
                try {
                    if(result.Children.Any(x => x.GetValue(DelayArg) < 0)) {
                        result.AddError("Delay should be higher or equal than 0ms");
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

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"{description}, ip: {ip}, порт: {port}, delay: {delay}");

                while(!applicationStopping.IsCancellationRequested) {
                    using var udpServer = new UdpClient(new IPEndPoint(IPAddress.Parse(ip), port));
                    uint IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    udpServer.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                    udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    try {
                        var logTimer = DateTime.Now.AddSeconds(1);
                        while(!applicationStopping.IsCancellationRequested) {
                            var result = await udpServer.ReceiveAsync(applicationStopping);
                            if(logTimer <= DateTime.Now) {
                                logTimer = DateTime.Now.AddSeconds(1);
                                logger.Information($"udp({port}) request from {result.RemoteEndPoint}, bytes:{result.Buffer.ToShortDescriptions()}");
                            }
                            if(delay > 0) {
                                await Task.Delay(delay);
                            }
                            var txCount = await udpServer.SendAsync(result.Buffer, result.RemoteEndPoint, applicationStopping);
                        }
                    } catch(SocketException ex) {
                        logger.Error(ex.Message);
                    }
                }

                return 0;
            });
        }
    }
}
