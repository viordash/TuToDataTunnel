﻿using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Тестовый udp-клиент") {
            Add(new Argument<string>("ip", "Remote UDP IP address"));
            Add(new Argument<int>("port", "Remote UDP IP port"));
            Add(new Option<bool>("--response", () => false));
        }

        public new class Handler : ICommandHandler {
            readonly ILogger logger;
            readonly IHostApplicationLifetime applicationLifetime;

            public string Ip { get; set; } = string.Empty;
            public int Port { get; set; }
            public bool Response { get; set; }

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

                while(!applicationLifetime.ApplicationStopping.IsCancellationRequested) {
                    var dataPacket = Enumerable.Repeat(Guid.NewGuid().ToByteArray(), 10).SelectMany(x => x).ToArray();
                    var txCount = await udpServer.SendAsync(dataPacket, remoteEndPoint, applicationLifetime.ApplicationStopping);
                    logger.Information($"udp({localPort}) request to {remoteEndPoint}, bytes:{txCount}");

                    if(Response) {
                        try {
                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping);
                            cts.CancelAfter(TimeSpan.FromMilliseconds(5000));

                            var result = await udpServer.ReceiveAsync(cts.Token);
                            if(dataPacket.SequenceEqual(result.Buffer)) {
                                logger.Information($"udp({localPort}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}. Success");
                            } else {
                                logger.Warning($"udp({localPort}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}. Wrong");
                            }
                        } catch(OperationCanceledException) {
                            logger.Warning($"udp({localPort}) response timeout");
                        }
                    }

                    await Task.Delay(1000);
                }

                _ = applicationLifetime.ApplicationStopping.WaitHandle.WaitOne();
                return 0;
            }
        }
    }
}
