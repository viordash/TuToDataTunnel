using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient<Socket> {
        int localPort;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }
        public bool Listening { get; private set; } = false;

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, Action<int, int> timeoutAction)
            : base(serverEndPoint, originPort, logger, timeoutAction) {
        }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            tcpClient.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            tcpClient.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
            tcpClient.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);

            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationToken);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            }
            var txCount = await socket.SendAsync(payload, SocketFlags.None, cancellationToken);

            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{payload.ToShortDescriptions()}");
            }
        }

        public void Listen(TransferTcpRequestModel request, ISignalRClient dataTunnelClient, CancellationToken cancellationToken) {
            if(Listening) {
                throw new TuToException($"tcp 0, port: {request.Payload.Port}, o-port: {request.Payload.OriginPort}, already listening");
            }
            Listening = true;
            _ = Task.Run(async () => {
                Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
                try {
                    while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                        var receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                        if(receivedBytes == 0) {
                            break;
                        }
                        var data = receiveBuffer[..receivedBytes].ToArray();
                        var transferResponse = new TransferTcpResponseModel(request, new TcpDataResponseModel(request.Payload.Port, request.Payload.OriginPort, data));
                        await dataTunnelClient.SendTcpResponse(transferResponse, cancellationToken);

                        if(responseLogTimer <= DateTime.Now) {
                            responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                            logger.Information($"tcp({localPort}) response from {socket.RemoteEndPoint}, bytes:{data.ToShortDescriptions()}.");
                        }
                        Refresh();
                    }
                    Listening = false;
                    logger.Information($"tcp({localPort}) disconnected from {socket.RemoteEndPoint}");
                } catch(SocketException ex) {
                    Listening = false;

                    logger.Error($"tcp({localPort}), o-port: {OriginPort}, error:  {ex.Message}");
                } catch {
                    Listening = false;
                    throw;
                }
                socket.Close();
                var transferCommand = new TransferTcpCommandModel(request.Id, request.Created, new TcpCommandModel(request.Payload.Port, request.Payload.OriginPort, SocketCommand.Disconnect));
                await dataTunnelClient.SendTcpCommand(transferCommand, cancellationToken);
            });
        }

        public override void Dispose() {
            base.Dispose();
            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed");
        }



        public async Task CreateStream(ChannelReader<byte[]> channel, CancellationToken cancellationToken) {
            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationToken);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            }

            // Wait asynchronously for data to become available
            while(await channel.WaitToReadAsync()) {
                // Read all currently available data synchronously, before waiting for more data
                while(channel.TryRead(out var count)) {
                    Console.WriteLine($"{count?.ToShortDescriptions(true)}");

                    //if(requestLogTimer <= DateTime.Now) {
                    //    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    //    logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{payload.ToShortDescriptions()}");
                    //}
                }
            }

            Console.WriteLine("Streaming completed");


        }
    }
}
