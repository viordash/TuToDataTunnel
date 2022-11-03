using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Communication {
    public class HubClient : IDisposable {
        public IClientProxy ClientProxy { get; private set; }
        public IEnumerable<int>? TcpPorts { get; private set; }
        public IEnumerable<int>? UdpPorts { get; private set; }

        readonly Dictionary<int, TcpServer> tcpServers = new();
        readonly Dictionary<int, UdpServer> udpServers = new();
        readonly CancellationTokenSource cts;
        readonly ILogger logger;

        public HubClient(IPEndPoint localEndPoint, IClientProxy clientProxy, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts,
                    IServiceProvider serviceProvider) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            cts = new CancellationTokenSource();

            var dataTransferService = serviceProvider.GetRequiredService<IDataTransferService>();
            logger = serviceProvider.GetRequiredService<ILogger>();
            if(tcpPorts != null) {
                tcpServers = tcpPorts
                    .ToDictionary(k => k, v => new TcpServer(v, localEndPoint, dataTransferService, logger));
            } else {
                tcpServers = new();
            }

            if(udpPorts != null) {
                udpServers = udpPorts
                    .ToDictionary(k => k, v => new UdpServer(v, localEndPoint, dataTransferService, logger, UdpSocketParams.ReceiveTimeout));
            } else {
                udpServers = new();
            }
        }

        public void Listen() {
            if(tcpServers != null) {
                Task.WhenAll(tcpServers.Values.Select(x => x.Listen()));
            }
            if(udpServers != null) {
                Task.WhenAll(udpServers.Values.Select(x => x.Listen()));
            }
        }


        public async Task SendUdpResponse(UdpDataResponseModel response) {
            if(!udpServers.TryGetValue(response.Port, out UdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, response.Port);
            }
            await server.SendResponse(response);
        }

        public Task ProcessUdpCommand(UdpCommandModel command) {
            if(!udpServers.TryGetValue(command.Port, out UdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, command.Port);
            }

            switch(command.Command) {
                case SocketCommand.Disconnect:
                    server.Disconnect(command);
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }


        public IAsyncEnumerable<byte[]> TcpStream2Cln(TcpStreamParam streamParam) {
            if(!tcpServers.TryGetValue(streamParam.Port, out TcpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Tcp, streamParam.Port);
            }
            return server.CreateStream(streamParam);
        }

        public async Task TcpStream2Srv(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream) {
            if(!tcpServers.TryGetValue(streamParam.Port, out TcpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Tcp, streamParam.Port);
            }
            await server.AcceptClientStream(streamParam, stream);
        }


        public async IAsyncEnumerable<TcpStreamDataModel> StreamToTcpClient([EnumeratorCancellation] CancellationToken cancellationToken = default) {

            var coopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            while(!coopCts.IsCancellationRequested) {
                await Task.Delay(200);

                var data = new TcpStreamDataModel(1, 1, Enumerable.Range(0, 200).Select(x => (byte)x).ToArray());
                logger.Information($"tcp request {data}");

                yield return data;
            }
        }

        public async Task StreamFromTcpClient(IAsyncEnumerable<TcpStreamDataModel> stream) {
            try {
                await foreach(var data in stream) {
                    logger.Information($"tcp response {data}");
                }

            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
            }
        }

        public void Dispose() {
            cts.Cancel();
            cts.Dispose();
            foreach(var item in tcpServers.Values) {
                item.Dispose();
            }
            foreach(var item in udpServers.Values) {
                item.Dispose();
            }
        }
    }
}
