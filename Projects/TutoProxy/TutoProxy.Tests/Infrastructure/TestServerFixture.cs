using System.Net;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core;

namespace TutoProxy.Tests.Infrastructure;

public class TestServerFixture : IAsyncDisposable {
    WebApplication? app;
    HubConnection? clientConnection;
    static int portCounter = 15000;

    public string ServerUrl { get; private set; } = null!;
    public int TcpPort { get; private set; }
    public int UdpPort { get; private set; }
    public HubConnection ClientConnection => clientConnection!;
    public IServiceProvider ServerServices => app!.Services;

    public async Task Start(int tcpPort = 0, int udpPort = 0, int serverPort = 0) {
        // Use unique ports for each test to avoid conflicts
        TcpPort = tcpPort == 0 ? Interlocked.Increment(ref portCounter) : tcpPort;
        UdpPort = udpPort == 0 ? Interlocked.Increment(ref portCounter) : udpPort;
        serverPort = serverPort == 0 ? Interlocked.Increment(ref portCounter) : serverPort;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{serverPort}");

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger());

        builder.Services
            .AddSignalR()
            .AddHubOptions<SignalRHub>(options => {
                options.MaximumReceiveMessageSize = 512 * 1024;
                options.MaximumParallelInvocationsPerClient = 512;
            })
            .AddMessagePackProtocol(options => {
                options.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(StandardResolver.Instance)
                    .WithSecurity(MessagePackSecurity.UntrustedData);
            });

        builder.Services.AddSingleton<Serilog.ILogger>(sp =>
            new LoggerConfiguration().MinimumLevel.Warning().CreateLogger());
        builder.Services.AddSingleton<IDataTransferService, DataTransferService>();
        builder.Services.AddSingleton<IProcessMonitor, ProcessMonitor>();
        builder.Services.AddSingleton<IServerFactory, ServerFactory>();
        builder.Services.AddSingleton<IHubClientsService>(sp => new HubClientsService(
            sp.GetRequiredService<Serilog.ILogger>(),
            sp.GetRequiredService<IHostApplicationLifetime>(),
            sp.GetRequiredService<IServiceProvider>(),
            sp.GetRequiredService<IProcessMonitor>(),
            new IPEndPoint(IPAddress.Loopback, 0),
            new List<int> { TcpPort },
            new List<int> { UdpPort },
            null));

        app = builder.Build();
        app.MapHub<SignalRHub>(SignalRParams.Path);

        await app.StartAsync();

        ServerUrl = app.Urls.First();
    }

    public async Task<HubConnection> ConnectClient(CancellationToken cancellationToken = default) {
        var uri = new UriBuilder(ServerUrl) {
            Path = SignalRParams.Path,
            Query = $"{SignalRParams.TcpQuery}={TcpPort}&{SignalRParams.UdpQuery}={UdpPort}"
        };

        clientConnection = new HubConnectionBuilder()
            .WithUrl(uri.Uri)
            .AddMessagePackProtocol(config => {
                config.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(StandardResolver.Instance)
                    .WithSecurity(MessagePackSecurity.UntrustedData);
            })
            .Build();

        await clientConnection.StartAsync(cancellationToken);
        return clientConnection;
    }

    public async ValueTask DisposeAsync() {
        if(clientConnection != null) {
            await clientConnection.DisposeAsync();
        }
        if(app != null) {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
