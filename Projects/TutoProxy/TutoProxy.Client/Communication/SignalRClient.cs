using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Communication {
    public interface ISignalRClient : IDisposable {
        Task StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, CancellationToken cancellationToken);
        Task StopAsync();
        Task SendUdpResponse(TransferUdpResponseModel response, CancellationToken cancellationToken);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken);

        Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken);
        void PushOutgoingTcpData(TcpStreamDataModel streamData, CancellationToken cancellationToken);
    }

    internal class SignalRClient : ISignalRClient {
        #region inner classes
        class RetryPolicy : IRetryPolicy {
            readonly ILogger logger;
            public RetryPolicy(ILogger logger) {
                this.logger = logger;
            }
            public TimeSpan? NextRetryDelay(RetryContext retryContext) {
                logger.Warning(retryContext.RetryReason?.Message);
                return TimeSpan.FromSeconds(Math.Min(retryContext.PreviousRetryCount + 1, 60));
            }
        }
        #endregion

        readonly ILogger logger;
        readonly IDataExchangeService dataExchangeService;
        readonly IClientsService clientsService;
        readonly BlockingCollection<TcpStreamDataModel> outgoingQueue;
        HubConnection? connection = null;

        public SignalRClient(
                ILogger logger,
                IDataExchangeService dataExchangeService,
                IClientsService clientsService
                ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataExchangeService, nameof(dataExchangeService));
            this.logger = logger;
            this.dataExchangeService = dataExchangeService;
            this.clientsService = clientsService;
            outgoingQueue = new BlockingCollection<TcpStreamDataModel>(new ConcurrentQueue<TcpStreamDataModel>(), TcpSocketParams.QueueMaxSize);
        }

        public void Dispose() {
        }

        public async Task StartAsync(string server, string? tcpQuery, string? udpQuery, string? clientId, CancellationToken cancellationToken) {
            Guard.NotNullOrEmpty(server, nameof(server));
            Guard.NotNull(tcpQuery ?? udpQuery, $"Tcp ?? Udp");

            await StopAsync();

            var ub = new UriBuilder(server);
            ub.Path = SignalRParams.Path;

            var query = QueryString.Create(new[] {
                KeyValuePair.Create(SignalRParams.TcpQuery, tcpQuery),
                KeyValuePair.Create(SignalRParams.UdpQuery, udpQuery),
                KeyValuePair.Create(SignalRParams.ClientId, clientId)
            });
            ub.Query = query.ToString();

            connection = new HubConnectionBuilder()
                 .WithUrl(ub.Uri)
                 .WithAutomaticReconnect(new RetryPolicy(logger))
                 .Build();

            connection.On<TransferUdpRequestModel>("UdpRequest", async (request) => {
                await dataExchangeService.HandleUdpRequest(request, this, cancellationToken);
            });

            connection.On<SocketAddressModel, Int64>("DisconnectUdp", (socketAddress, totalTransfered) => {
                logger.Debug($"HandleDisconnectUdp :{socketAddress}, {totalTransfered}");
                var client = clientsService.ObtainUdpClient(socketAddress.Port, socketAddress.OriginPort, this);
                client.Disconnect(totalTransfered);
            });

            connection.On<SocketAddressModel, Int64>("DisconnectTcp", (socketAddress, totalTransfered) => {
                logger.Debug($"HandleDisconnectTcp :{socketAddress}, {totalTransfered}");
                Debug.WriteLine($"client HandleDisconnectTcp :{socketAddress}, {totalTransfered}");
                var client = clientsService.ObtainTcpClient(socketAddress.Port, socketAddress.OriginPort, this);
                client.Disconnect(totalTransfered, cancellationToken);
            });

            connection.On<string>("Errors", async (message) => {
                logger.Error(message);
                await StopAsync();
            });

            connection.Reconnecting += e => {
                logger.Warning($"Connection lost. Reconnecting");
                return Task.CompletedTask;
            };

            connection.Reconnected += s => {
                logger.Information($"Connection reconnected: {s}");
                return Task.CompletedTask;
            };

            await connection.StartAsync(cancellationToken);
            logger.Information("Connection started");

            StartStreamToTcpClient(cancellationToken);
            StartStreamFromTcpClient(cancellationToken);
        }

        public async Task StopAsync() {
            if(connection != null) {
                await connection.DisposeAsync();
                logger.Information("Connection stopped");
                connection = null;
            }
        }

        public async Task SendUdpResponse(TransferUdpResponseModel response, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.InvokeAsync("UdpResponse", response, cancellationToken);
            }
        }

        public async Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                await connection.InvokeAsync("DisconnectUdp", socketAddress, totalTransfered, cancellationToken);
            }
        }

        public async Task DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered, CancellationToken cancellationToken) {
            if(connection?.State == HubConnectionState.Connected) {
                Debug.WriteLine($"client DisconnectTcp :{socketAddress}, {totalTransfered}");
                await connection.InvokeAsync("DisconnectTcp", socketAddress, totalTransfered, cancellationToken);
            }
        }

        void StartStreamToTcpClient(CancellationToken cancellationToken) {
            _ = Task.Run(async () => {
                if(connection?.State != HubConnectionState.Connected) {
                    return;
                }
                var streamData = connection.StreamAsync<TcpStreamDataModel>("StreamToTcpClient", cancellationToken);

                await foreach(var data in streamData) {
                    try {
                        var client = clientsService.ObtainTcpClient(data.Port, data.OriginPort, this);
                        await client.SendData(data, cancellationToken);
                    } catch(Exception ex) {
                        logger.Error(ex.GetBaseException().Message);
                    }
                }
                Debug.WriteLine($"                  ------ client stopped");
            }, cancellationToken);
        }

        async IAsyncEnumerable<TcpStreamDataModel> OutgoingDataStream([EnumeratorCancellation] CancellationToken cancellationToken) {
            while(!cancellationToken.IsCancellationRequested) {
                TcpStreamDataModel? streamData = null;

                //Debug.WriteLine($"                  ------ client take 0 take_0: {outgoingQueue.Count}");
                streamData = await Task.Run(() => {
                    try {
                        return outgoingQueue.Take(cancellationToken);
                    } catch(InvalidOperationException) {
                        return null;
                    }
                }, cancellationToken);

                //Debug.WriteLine($"                  ------ client take 1 take_1: {outgoingQueue.Count}");
                if(streamData != null) {
                    yield return streamData;
                }
            }
        }

        void StartStreamFromTcpClient(CancellationToken cancellationToken) {
            _ = Task.Run(async () => {
                if(connection?.State == HubConnectionState.Connected) {
                    await connection.SendAsync("StreamFromTcpClient", OutgoingDataStream(cancellationToken), cancellationToken);
                }
            }, cancellationToken);
        }

        public void PushOutgoingTcpData(TcpStreamDataModel streamData, CancellationToken cancellationToken) {
            if(!outgoingQueue.TryAdd(streamData, 30000, cancellationToken)) {
                throw new TuToException($"tcp outcome queue size ({outgoingQueue.Count}) exceeds {TcpSocketParams.QueueMaxSize} limit");
            }
            //if(outgoingQueue.Count > 800) {
            //    Debug.WriteLine($"                  ------ client add 0: {outgoingQueue.Count}");
            //}
        }
    }
}
