using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
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
        readonly BlockingCollection<TcpStreamDataModel> outgoingQueue;
        uint counter = 0;

        public HubClient(IPEndPoint localEndPoint, IClientProxy clientProxy, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts,
                    IServiceProvider serviceProvider) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            cts = new CancellationTokenSource();
            outgoingQueue = new BlockingCollection<TcpStreamDataModel>(new ConcurrentQueue<TcpStreamDataModel>(), TcpSocketParams.QueueMaxSize);

            var dataTransferService = serviceProvider.GetRequiredService<IDataTransferService>();
            logger = serviceProvider.GetRequiredService<ILogger>();
            if(tcpPorts != null) {
                tcpServers = tcpPorts
                    .ToDictionary(k => k, v => new TcpServer(v, localEndPoint, dataTransferService, this, logger));
            } else {
                tcpServers = new();
            }

            if(udpPorts != null) {
                udpServers = udpPorts
                    .ToDictionary(k => k, v => new UdpServer(v, localEndPoint, dataTransferService, this, logger, UdpSocketParams.ReceiveTimeout));
            } else {
                udpServers = new();
            }
        }

        public void Dispose() {
            cts.Cancel();
            outgoingQueue.Dispose();
            cts.Dispose();

            foreach(var item in tcpServers.Values) {
                item.Dispose();
            }
            foreach(var item in udpServers.Values) {
                item.Dispose();
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

        public async IAsyncEnumerable<TcpStreamDataModel> StreamToTcpClient([EnumeratorCancellation] CancellationToken cancellationToken = default) {

            var coopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            while(!coopCts.IsCancellationRequested && !outgoingQueue.IsCompleted) {
                TcpStreamDataModel? streamData = null;

                streamData = await Task.Run(() => {
                    try {
                        return outgoingQueue.Take(coopCts.Token);
                    } catch(InvalidOperationException) {
                        return null;
                    }
                }, coopCts.Token);

                //Debug.WriteLine($"    ------ server take: {outgoingQueue.Count}");
                if(streamData != null) {
                    yield return streamData;
                }
            }
        }

        public async Task StreamFromTcpClient(IAsyncEnumerable<TcpStreamDataModel> stream) {
            await foreach(var data in stream) {
                try {
                    if(tcpServers.TryGetValue(data.Port, out TcpServer? server)) {
                        await server.SendData(data);
                    } else {
                        logger.Error($"tcp server {data.Port} not found");
                    }
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                }
            }
            Debug.WriteLine($"                  ------ server stopped");
        }

        public void PushOutgoingTcpData(TcpStreamDataModel streamData) {
            if(!outgoingQueue.TryAdd(streamData, 10000, cts.Token)) {
                throw new TuToException($"tcp outcome queue size exceeds {TcpSocketParams.QueueMaxSize} limit");
            }
            //Debug.WriteLine($"    ------ server add: {outgoingQueue.Count}");
        }
    }
}
