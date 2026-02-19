using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using NUnit.Framework;
using TutoProxy.Tests.Infrastructure;
using TuToProxy.Core;
using TuToProxy.Core.Models;

namespace TutoProxy.Tests.IntegrationTests;

[TestFixture]
public class TcpIntegrationTests {

    [Test]
    public async Task ServerClientConnection_ShouldEstablish() {
        await using var fixture = new TestServerFixture();
        await fixture.Start();

        var connection = await fixture.ConnectClient();

        Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
    }

    [Test]
    public async Task TcpDataTransfer_ShouldWork() {
        await using var fixture = new TestServerFixture();
        await fixture.Start();

        var connection = await fixture.ConnectClient();

        var receivedData = new TaskCompletionSource<byte[]>();

        // Setup handler for TCP requests
        connection.On<TcpDataRequestModel, int>("TcpRequest", async (request) => {
            receivedData.TrySetResult(request.Data.ToArray());
            return request.Data.Length;
        });

        connection.On<SocketAddressModel, SocketError>("ConnectTcp", async (socketAddress) => {
            return SocketError.Success;
        });

        // Wait for handlers to be registered
        await Task.Delay(100);

        // Connect external TCP client to server's port
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, fixture.TcpPort);

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        await tcpClient.GetStream().WriteAsync(testData);

        // Wait for data to arrive via SignalR
        var result = await receivedData.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(result, Is.EqualTo(testData));
    }

    [Test]
    [Category("Benchmark")]
    public async Task TcpThroughput_Measure() {
        await using var fixture = new TestServerFixture();
        await fixture.Start();

        var connection = await fixture.ConnectClient();

        int packetsReceived = 0;
        long bytesReceived = 0;

        connection.On<TcpDataRequestModel, int>("TcpRequest", async (request) => {
            Interlocked.Increment(ref packetsReceived);
            Interlocked.Add(ref bytesReceived, request.Data.Length);
            return request.Data.Length;
        });

        connection.On<SocketAddressModel, SocketError>("ConnectTcp", async (socketAddress) => {
            return SocketError.Success;
        });

        await Task.Delay(100);

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, fixture.TcpPort);

        var testData = new byte[8192];
        Random.Shared.NextBytes(testData);

        var sw = Stopwatch.StartNew();
        const int iterations = 100; // Reduced for faster test

        for(int i = 0; i < iterations; i++) {
            await tcpClient.GetStream().WriteAsync(testData);
        }

        // Wait for packets to be processed
        var timeout = TimeSpan.FromSeconds(10);
        while(packetsReceived < iterations && sw.Elapsed < timeout) {
            await Task.Delay(50);
        }

        sw.Stop();

        var throughputMBps = (bytesReceived / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"TCP Throughput Test Results:");
        Console.WriteLine($"  Packets sent: {iterations}");
        Console.WriteLine($"  Packets received: {packetsReceived}");
        Console.WriteLine($"  Bytes: {bytesReceived:N0}");
        Console.WriteLine($"  Time: {sw.Elapsed.TotalMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {throughputMBps:N2} MB/s");

        // Just verify we received some packets
        Assert.That(packetsReceived, Is.GreaterThan(0), "Should receive at least some packets");
    }
}
