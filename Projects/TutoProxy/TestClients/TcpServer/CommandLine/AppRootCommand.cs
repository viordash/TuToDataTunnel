using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        const string description = "Тестовый tcp-сервер";
        public AppRootCommand() : base(description) {
            Add(new Argument<string>("ip", "Listen TCP IP address"));
            Add(new Argument<int>("port", "Listen TCP IP port"));
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
                    var tcpServer = new TcpListener(IPAddress.Parse(Ip), Port);
                    tcpServer.Start();
                    try {
                        while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                            var socket = await tcpServer.AcceptSocketAsync(applicationLifetime.ApplicationStopping);

                            logger.Information($"tcp accept {socket.RemoteEndPoint}");
                            _ = Task.Run(async () => await HandleSocketAsync(socket, applicationLifetime.ApplicationStopping));
                        }
                    } catch(SocketException ex) {
                        logger.Error(ex.Message);
                        try {
                            tcpServer.Stop();
                        } catch { }
                    }
                }
                return 0;
            }

            async Task HandleSocketAsync(Socket socket, CancellationToken cancellationToken) {
                Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

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
                            logger.Information($"tcp({Port}) request from {(IPEndPoint)socket.RemoteEndPoint!}, bytes:{data.ToShortDescriptions()}");
                        }
                        if(Delay > 0) {
                            await Task.Delay(Delay);
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
}
