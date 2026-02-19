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
public class UdpIntegrationTests {

    [Test]
    public async Task UdpDataTransfer_ShouldWork() {
        await using var fixture = new TestServerFixture();
        await fixture.Start();

        var connection = await fixture.ConnectClient();

        var receivedData = new TaskCompletionSource<byte[]>();

        // Setup handler for UDP requests
        connection.On<UdpDataRequestModel>("UdpRequest", (request) => {
            receivedData.TrySetResult(request.Data.ToArray());
        });

        // Wait for handlers to be registered
        await Task.Delay(100);

        // Send UDP packet to server's port
        using var udpClient = new UdpClient();
        var testData = new byte[] { 10, 20, 30, 40, 50 };
        await udpClient.SendAsync(testData, new IPEndPoint(IPAddress.Loopback, fixture.UdpPort));

        // Wait for data to arrive via SignalR
        var result = await receivedData.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(result, Is.EqualTo(testData));
    }

    [Test]
    [Category("Benchmark")]
    public async Task UdpThroughput_Measure() {
        await using var fixture = new TestServerFixture();
        await fixture.Start();

        var connection = await fixture.ConnectClient();

        int packetsReceived = 0;
        long bytesReceived = 0;

        connection.On<UdpDataRequestModel>("UdpRequest", (request) => {
            Interlocked.Increment(ref packetsReceived);
            Interlocked.Add(ref bytesReceived, request.Data.Length);
        });

        await Task.Delay(100);

        using var udpClient = new UdpClient();
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, fixture.UdpPort);

        var testData = new byte[1024];
        Random.Shared.NextBytes(testData);

        var sw = Stopwatch.StartNew();
        const int iterations = 500; // Reduced for faster test

        for(int i = 0; i < iterations; i++) {
            await udpClient.SendAsync(testData, serverEndpoint);
            // Small delay to avoid overwhelming the server
            if(i % 50 == 0) await Task.Delay(1);
        }

        // Wait for packets to be processed
        await Task.Delay(1000);

        sw.Stop();

        var throughputMBps = (bytesReceived / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
        var packetLoss = (iterations - packetsReceived) * 100.0 / iterations;

        Console.WriteLine($"UDP Throughput Test Results:");
        Console.WriteLine($"  Packets sent: {iterations}");
        Console.WriteLine($"  Packets received: {packetsReceived}");
        Console.WriteLine($"  Packet loss: {packetLoss:N1}%");
        Console.WriteLine($"  Bytes: {bytesReceived:N0}");
        Console.WriteLine($"  Time: {sw.Elapsed.TotalMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {throughputMBps:N2} MB/s");

        // Just verify we received some packets
        Assert.That(packetsReceived, Is.GreaterThan(0), "Should receive at least some packets");
    }
}
