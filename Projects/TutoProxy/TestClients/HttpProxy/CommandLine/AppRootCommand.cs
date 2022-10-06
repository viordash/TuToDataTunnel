using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using HttpMachine;
using IHttpMachine.Model;
using Microsoft.Extensions.Hosting;

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
                Memory<byte> receiveBuffer = new byte[65536];

                var clientToProxyStream = new NetworkStream(socket);

                // читаем HTTP запрос от клиента
                int receivedBytes = await clientToProxyStream.ReadAsync(receiveBuffer, cancellationToken);
                if(receivedBytes == 0) {
                    return;
                }

                var bytes = receiveBuffer[..receivedBytes].ToArray();

                var request = Encoding.ASCII.GetString(bytes);
                logger.Information($"request: {request}");

                using(var handler = new HttpParserDelegate())
                using(var parser = new HttpCombinedParser(handler)) {
                    if(parser.Execute(bytes) != bytes.Length) {
                        throw new Exception("HttpParser error");
                    }

                    switch(handler.HttpRequestResponse.Method) {
                        case "CONNECT":
                            await HttpsProxyAsync(clientToProxyStream, handler.HttpRequestResponse, cancellationToken);
                            break;

                        default:
                            await HttpProxyAsync(clientToProxyStream, handler.HttpRequestResponse, cancellationToken);
                            break;
                    }
                }
                logger.Information($"tcp disconnected {socket.RemoteEndPoint}");
            }


            async Task HttpsProxyAsync(NetworkStream clientToProxyStream, HttpRequestResponse httpRequestResponse, CancellationToken cancellationToken) {
                var uri = new Uri("http://" + httpRequestResponse.RequestUri);

                var client = new TcpClient(uri.Host, uri.Port);
                var proxyToStackoverflowStream = client.GetStream();

                string response = $"HTTP/{httpRequestResponse.MajorVersion}.{httpRequestResponse.MinorVersion} 200 Connection established\r\n\r\n";
                var bytes = Encoding.ASCII.GetBytes(response);
                await clientToProxyStream.WriteAsync(bytes);
                var task1 = clientToProxyStream.CopyToAsync(proxyToStackoverflowStream, cancellationToken);
                var task2 = proxyToStackoverflowStream.CopyToAsync(clientToProxyStream, cancellationToken);
                await Task.WhenAll(task1, task2);
            }

            async Task HttpProxyAsync(NetworkStream clientToProxyStream, HttpRequestResponse httpRequestResponse, CancellationToken cancellationToken) {



                //[01:48:54 I]request: POST http://tuto24.com/test HTTP/1.1
                //User-Agent: PostmanRuntime/7.29.2
                //Accept: */*
                //Cache-Control: no-cache
                //Postman-Token: d2fcbb01-e946-411b-9b55-7e932cc31469
                //Host: tuto24.com
                //Accept-Encoding: gzip, deflate, br
                //Connection: keep-alive
                //Cookie: PHPSESSID=10t68rqn5e0q04dje72oms8i30
                //Content-Length: 0

            }

        }
    }
}
