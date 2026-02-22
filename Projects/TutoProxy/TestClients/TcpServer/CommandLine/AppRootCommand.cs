using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        const string description = "Тестовый tcp-сервер";

        public Argument<string> IpArg { get; }
        public Argument<int> PortArg { get; }
        public Argument<int> DelayArg { get; }

        public AppRootCommand() : base(description) {
            IpArg = new Argument<string>("ip") { Description = "Listen TCP IP address" };
            PortArg = new Argument<int>("port") { Description = "Listen TCP IP port" };
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
                    var tcpServer = new TcpListener(IPAddress.Parse(ip), port);
                    tcpServer.Start();
                    try {
                        while(!applicationStopping.IsCancellationRequested) {
                            var socket = await tcpServer.AcceptSocketAsync(applicationStopping);

                            logger.Information($"tcp accept {socket.RemoteEndPoint}");
                            _ = Task.Run(async () => await HandleSocketAsync(socket, delay, logger, applicationStopping));
                        }
                    } catch(SocketException ex) {
                        logger.Error(ex.Message);
                        try {
                            tcpServer.Stop();
                        } catch { }
                    }
                }
                return 0;
            });
        }

        static async Task HandleSocketAsync(Socket socket, int delay, Serilog.ILogger logger, CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            var port = (socket.LocalEndPoint as IPEndPoint)!.Port;

            try {
                var logTimer = DateTime.Now.AddSeconds(1);
                while(socket.Connected) {
                    var receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                    var data = receiveBuffer[..receivedBytes].ToArray();
                    if(logTimer <= DateTime.Now) {
                        logTimer = DateTime.Now.AddSeconds(1);
                        logger.Information($"tcp({port}) request from {(IPEndPoint)socket.RemoteEndPoint!}, bytes:{data.ToShortDescriptions()}");
                    }
                    if(delay > 0) {
                        await Task.Delay(delay);
                    }
                    var txCount = await socket.SendAsync(data, SocketFlags.None, cancellationToken);
                }
            } catch(SocketException ex) {
                logger.Error($"socket: {ex.Message}");
            }
            logger.Information($"tcp disconnected {socket.RemoteEndPoint}");
        }
    }
}
