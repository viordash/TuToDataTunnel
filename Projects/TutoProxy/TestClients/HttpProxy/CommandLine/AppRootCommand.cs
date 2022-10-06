using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HttpMachine;
using IHttpMachine.Model;
using Microsoft.Extensions.Hosting;
using static TutoProxy.Server.CommandLine.AppRootCommand;

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

                var clientToProxyStream = new NetworkStream(socket, true);

                int receivedBytes = await clientToProxyStream.ReadAsync(receiveBuffer, cancellationToken);
                if(receivedBytes == 0) {
                    return;
                }

                var bytes = receiveBuffer[..receivedBytes].ToArray();

                var request = Encoding.ASCII.GetString(bytes);
                logger.Information($"request: {request.Length}");

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
                            await HttpProxyAsync(clientToProxyStream, handler.HttpRequestResponse, request, cancellationToken);
                            break;
                    }
                }
                logger.Information($"tcp disconnected {socket.RemoteEndPoint}");
                socket.Dispose();
            }


            static async Task<bool> WaitData(NetworkStream networkStream) {
                var timer = DateTime.Now.AddMilliseconds(1000);
                while(networkStream.CanRead && !networkStream.DataAvailable && timer > DateTime.Now) {
                    await Task.Delay(1);
                }
                bool dataAvailable = networkStream.DataAvailable;
                return dataAvailable;
            }

            async Task HttpsProxyAsync(NetworkStream clientToProxyStream, HttpRequestResponse httpRequestResponse, CancellationToken cancellationToken) {
                var uri = new Uri("http://" + httpRequestResponse.RequestUri);

                using var client = new TcpClient(uri.Host, uri.Port);
                var proxyToClientStream = client.GetStream();

                var response = $"HTTP/{httpRequestResponse.MajorVersion}.{httpRequestResponse.MinorVersion} 200 Connection established\r\n\r\n";
                var bytes = Encoding.ASCII.GetBytes(response);
                await clientToProxyStream.WriteAsync(bytes);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(3000));

                var task1 = clientToProxyStream.CopyToAsync(proxyToClientStream, cts.Token);
                var task2 = proxyToClientStream.CopyToAsync(clientToProxyStream, cts.Token);
                await Task.WhenAll(task1, task2);

                //int counter = 0;
                //Memory<byte> receiveBuffer = new byte[65536];
                //do {
                //    int receivedBytes = await clientToProxyStream.ReadAsync(receiveBuffer, cancellationToken);
                //    if(receivedBytes == 0) {
                //        return;
                //    }

                //    logger.Information($"request: {Encoding.ASCII.GetString(receiveBuffer[..receivedBytes].ToArray()).Length}, cnt: {counter++}");

                //    await proxyToClientStream.WriteAsync(receiveBuffer[..receivedBytes], cancellationToken);
                //    //await proxyToClientStream.FlushAsync(cancellationToken);

                //    do {
                //        int clientReceivedBytes = await proxyToClientStream.ReadAsync(receiveBuffer, cancellationToken);
                //        if(clientReceivedBytes == 0) {
                //            return;
                //        }
                //        logger.Information($"response: {Encoding.ASCII.GetString(receiveBuffer[..clientReceivedBytes].ToArray()).Length}, cnt: {counter++}");

                //        await clientToProxyStream.WriteAsync(receiveBuffer[..clientReceivedBytes]);
                //        //await clientToProxyStream.FlushAsync(cancellationToken);

                //    } while(await WaitData(proxyToClientStream));

                //} while(await WaitData(clientToProxyStream));

                await clientToProxyStream.Socket.DisconnectAsync(true, cancellationToken);
            }

            async Task HttpProxyAsync(NetworkStream clientToProxyStream, HttpRequestResponse httpRequestResponse, string request,
                    CancellationToken cancellationToken) {

                var uri = new Uri(httpRequestResponse.RequestUri);

                using var client = new TcpClient(uri.Host, uri.Port);
                //client.Connect(uri.Host, uri.Port);
                //client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                //client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 2000);
                //client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 2000);
                //client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 4);

                //int size = Marshal.SizeOf((uint)0);
                //byte[] keepAlive = new byte[size * 3];
                //// Pack the byte array:
                //// Turn keepalive on
                //Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, size);
                //// Set amount of time without activity before sending a keepalive to 5 seconds
                //Buffer.BlockCopy(BitConverter.GetBytes((uint)5000), 0, keepAlive, size, size);
                //// Set keepalive interval to 5 seconds
                //Buffer.BlockCopy(BitConverter.GetBytes((uint)15000), 0, keepAlive, size * 2, size);

                //// Set the keep-alive settings on the underlying Socket
                //client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);


                var proxyToClientStream = client.GetStream();

                var requestToClient = request.Replace(httpRequestResponse.RequestUri, uri.PathAndQuery);
                //requestToClient = requestToClient.Replace("Connection: keep-alive", "Connection: close");

                logger.Information($"requestToClient: {requestToClient.Length}");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(1000));

                var bytes = Encoding.ASCII.GetBytes(requestToClient);
                try {
                    await proxyToClientStream.WriteAsync(bytes, cts.Token);
                    await proxyToClientStream.CopyToAsync(clientToProxyStream, cts.Token);

                } catch(Exception e) {
                    //    logger.Error(e.GetBaseException().ToString());
                }

                //int counter = 0;
                //Memory<byte> receiveBuffer = new byte[65536];
                //do {
                //    int receivedBytes = await proxyToClientStream.ReadAsync(receiveBuffer, cancellationToken);
                //    if(receivedBytes == 0) {
                //        return;
                //    }

                //    logger.Information($"request: {Encoding.ASCII.GetString(receiveBuffer[..receivedBytes].ToArray()).Length}, cnt: {counter++}");

                //    await clientToProxyStream.WriteAsync(receiveBuffer[..receivedBytes]);

                //} while(await WaitData(proxyToClientStream));

                await clientToProxyStream.Socket.DisconnectAsync(true, cancellationToken);
            }

        }
    }
}
