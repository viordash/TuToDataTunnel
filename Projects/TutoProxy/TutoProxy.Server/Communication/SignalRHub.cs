using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Hubs {
    public class SignalRHub : Hub {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;
        readonly IHubClientsService clientsService;
        bool connected;

        public SignalRHub(
                ILogger logger,
                IDataTransferService dataTransferService,
                IHubClientsService clientsService) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(clientsService, nameof(clientsService));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
            this.clientsService = clientsService;
            connected = false;
        }

        public async Task UdpResponse(TransferUdpResponseModel model) {
            logger.Debug($"UdpResponse: {model}");
            try {
                await dataTransferService.HandleUdpResponse(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public async Task UdpCommand(TransferUdpCommandModel model) {
            logger.Debug($"UdpCommand: {model}");
            try {
                await dataTransferService.HandleUdpCommand(Context.ConnectionId, model);
            } catch(TuToException ex) {
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
        }

        public override async Task OnConnectedAsync() {
            try {
                var queryString = Context.GetHttpContext()?.Request.QueryString.Value;
                clientsService.Connect(Context.ConnectionId, Clients.Caller, queryString);
            } catch(TuToException ex) {
                logger.Error(ex.Message);
                await Clients.Caller.SendAsync("Errors", ex.Message);
            }
            await base.OnConnectedAsync();
            connected = true;
            _ = Task.Run(() => NamedPipeStreamToTcpClient());
        }

        public override Task OnDisconnectedAsync(Exception? exception) {
            connected = false;
            clientsService.Disconnect(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        IAsyncEnumerable<TcpStreamDataModel> StreamToTcpClient() {
            var client = clientsService.GetClient(""/*Context.ConnectionId*/);
            return client.StreamToTcpClient();
        }

        Task StreamFromTcpClient(IAsyncEnumerable<TcpStreamDataModel> stream) {
            var client = clientsService.GetClient(""/*Context.ConnectionId*/);
            return client.StreamFromTcpClient(stream);
        }

        NamedPipeServerStream? pipeStreamToTcpClient;
        public async Task NamedPipeStreamToTcpClient() {
            pipeStreamToTcpClient = new NamedPipeServerStream("testStreamToTcpClient", PipeDirection.InOut);
            await pipeStreamToTcpClient.WaitForConnectionAsync();


            Debug.WriteLine("[Server] Client connected.");


            _ = Task.Run(() => StreamFromTcpClient(NamedPipeStreamFromTcpClient()));

            try {
                // Read user input and send that to the client process.
                using(StreamWriter sw = new StreamWriter(pipeStreamToTcpClient)) {
                    sw.AutoFlush = true;

                    var stream = StreamToTcpClient();
                    await foreach(var data in stream) {
                        try {
                            var jsonString = JsonSerializer.Serialize(data);
                            await sw.WriteLineAsync(jsonString);
                        } catch(Exception ex) {
                            logger.Error(ex.GetBaseException().Message);
                        }
                    }
                }
            } catch(IOException e) {
                Debug.WriteLine("[Server] ERROR: {0}", e.Message);
            }
        }

        NamedPipeClientStream? pipeStreamFromTcpClient;
        public async IAsyncEnumerable<TcpStreamDataModel> NamedPipeStreamFromTcpClient() {
            pipeStreamFromTcpClient = new NamedPipeClientStream(".", "testStreamFromTcpClient", PipeDirection.InOut);
            await pipeStreamFromTcpClient.ConnectAsync();


            Debug.WriteLine("[Server] Connected to pipe.");
            using(StreamReader sr = new StreamReader(pipeStreamFromTcpClient)) {

                while(connected) {
                    var jsonString = await sr.ReadLineAsync();
                    if(jsonString != null) {
                        var streamData = JsonSerializer.Deserialize<TcpStreamDataModel>(jsonString);
                        if(streamData != null) {
                            yield return streamData;
                        }
                    }
                }
            }
        }


    }
}
