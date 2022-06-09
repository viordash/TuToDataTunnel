using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using TuToProxy.Core;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Тестовый tcp-сервер") {
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
                var tcpServer = new TcpListener(IPAddress.Any, Port);
                tcpServer.Start();
                while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                    var socket = await tcpServer.AcceptSocketAsync(applicationLifetime.ApplicationStopping);

                    logger.Information($"tcp accept {socket.RemoteEndPoint}");
                    _ = Task.Run(async () => await HandleSocketAsync(socket, applicationLifetime.ApplicationStopping));
                }
                return 0;
            }

            async Task HandleSocketAsync(Socket socket, CancellationToken cancellationToken) {
                Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
                var logTimer = DateTime.Now.AddSeconds(1);
                while(socket.Connected) {
                    var receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                    if(logTimer <= DateTime.Now) {
                        logTimer = DateTime.Now.AddSeconds(1);
                        logger.Information($"tcp({Port}) request from {(IPEndPoint)socket.RemoteEndPoint!}, bytes:{receivedBytes}");
                    }
                    if(Delay > 0) {
                        await Task.Delay(Delay);
                    }
                    var txCount = await socket.SendAsync(receiveBuffer[..receivedBytes], SocketFlags.None, cancellationToken);
                }
                logger.Information($"tcp disconnected {socket.RemoteEndPoint}");
            }
        }
    }
}
