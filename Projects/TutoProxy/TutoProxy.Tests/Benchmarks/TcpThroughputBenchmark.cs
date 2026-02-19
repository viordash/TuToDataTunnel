using System.Net;
using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.AspNetCore.SignalR.Client;
using TutoProxy.Tests.Infrastructure;
using TuToProxy.Core;
using TuToProxy.Core.Models;

namespace TutoProxy.Tests.Benchmarks;

[SimpleJob(RuntimeMoniker.HostProcess)]
[MemoryDiagnoser]
public class TcpThroughputBenchmark {
    TestServerFixture fixture = null!;
    TcpListener echoServer = null!;
    int echoServerPort;

    byte[] testData = null!;
    TaskCompletionSource<byte[]>? responseReceived;

    [Params(1024, 8192, 65536)]
    public int PayloadSize;

    [GlobalSetup]
    public async Task Setup() {
        testData = new byte[PayloadSize];
        Random.Shared.NextBytes(testData);

        // Start echo server (simulates target server that Client connects to)
        echoServer = new TcpListener(IPAddress.Loopback, 0);
        echoServer.Start();
        echoServerPort = ((IPEndPoint)echoServer.LocalEndpoint).Port;

        // Start accepting connections in background
        _ = Task.Run(async () => {
            while(true) {
                try {
                    var client = await echoServer.AcceptTcpClientAsync();
                    _ = HandleEchoClient(client);
                } catch(SocketException) {
                    break;
                }
            }
        });

        fixture = new TestServerFixture();
        await fixture.Start(tcpPort: echoServerPort, udpPort: 19000);

        // Connect SignalR client and setup handlers
        var connection = await fixture.ConnectClient();

        // Handle TCP requests from server - echo back
        connection.On<TcpDataRequestModel, int>("TcpRequest", async (request) => {
            responseReceived?.TrySetResult(request.Data.ToArray());
            return request.Data.Length;
        });

        connection.On<SocketAddressModel, SocketError>("ConnectTcp", async (socketAddress) => {
            return SocketError.Success;
        });

        connection.On<SocketAddressModel, bool>("DisconnectTcp", async (socketAddress) => {
            return true;
        });

        // Wait for server to be ready
        await Task.Delay(100);
    }

    async Task HandleEchoClient(TcpClient client) {
        using(client)
        using(var stream = client.GetStream()) {
            var buffer = new byte[65536];
            int bytesRead;
            while((bytesRead = await stream.ReadAsync(buffer)) > 0) {
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
            }
        }
    }

    [GlobalCleanup]
    public async Task Cleanup() {
        echoServer.Stop();
        await fixture.DisposeAsync();
    }

    [Benchmark]
    public async Task<int> SendTcpData() {
        responseReceived = new TaskCompletionSource<byte[]>();

        // Simulate external client connecting to server's TCP port
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, fixture.TcpPort);

        using var stream = client.GetStream();
        await stream.WriteAsync(testData);

        // Wait for response via SignalR
        var response = await responseReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return response.Length;
    }
}
