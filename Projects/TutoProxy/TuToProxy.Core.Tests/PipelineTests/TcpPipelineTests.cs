using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TuToProxy.Core.Models;
using TuToProxy.Core.Pipeline;

namespace TuToProxy.Core.Tests.PipelineTests {
    public class TcpPipelineTests {
        Socket serverSocket = null!;
        Socket clientSocket = null!;
        Socket acceptedSocket = null!;

        [SetUp]
        public async Task SetUp() {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            serverSocket.Listen(1);
            var port = ((IPEndPoint)serverSocket.LocalEndPoint!).Port;

            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var connectTask = clientSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
            acceptedSocket = await serverSocket.AcceptAsync();
            await connectTask;
        }

        [TearDown]
        public void TearDown() {
            acceptedSocket?.Close();
            clientSocket?.Close();
            serverSocket?.Close();
        }

        [Test]
        public async Task ReadFromSocket_InvokesCallback_WithBytesReceived() {
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            var bytesReceived = 0;

            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(acceptedSocket);

            using var cts = new CancellationTokenSource();

            var readTask = Task.Run(async () => {
                await pipeline.ReadFromSocket(bytes => bytesReceived += bytes, cts.Token);
            });

            await clientSocket.SendAsync(testData, SocketFlags.None);
            await Task.Delay(50);
            clientSocket.Shutdown(SocketShutdown.Both);

            await readTask;

            Assert.That(bytesReceived, Is.EqualTo(testData.Length));
        }

        [Test]
        public async Task FullPipeline_ProcessesData_AndCallsSender() {
            var testData = new byte[] { 10, 20, 30, 40, 50 };
            var sentModels = new List<TcpDataRequestModel>();
            var transmittedBytes = new List<int>();

            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(acceptedSocket);

            using var cts = new CancellationTokenSource();

            var pipelineTask = Task.Run(async () => {
                var readTask = pipeline.ReadFromSocket(null, cts.Token);
                var processTask = pipeline.ProcessPipe(8080, 12345, cts.Token);
                var sendTask = pipeline.SendRequests(
                    (model, ct) => {
                        sentModels.Add(new TcpDataRequestModel {
                            Port = model.Port,
                            OriginPort = model.OriginPort,
                            Data = model.Data.ToArray(),
                            SequenceNumber = model.SequenceNumber
                        });
                        return Task.FromResult(model.Data.Length);
                    },
                    (seq, transmitted) => transmittedBytes.Add(transmitted),
                    (seq, expected, actual) => { },
                    cts.Token
                );

                await Task.WhenAll(readTask, processTask, sendTask);
            });

            await clientSocket.SendAsync(testData, SocketFlags.None);
            await Task.Delay(100);
            clientSocket.Shutdown(SocketShutdown.Both);

            await pipelineTask;

            Assert.That(sentModels.Count, Is.GreaterThan(0));
            Assert.That(sentModels[0].Port, Is.EqualTo(8080));
            Assert.That(sentModels[0].OriginPort, Is.EqualTo(12345));
            Assert.That(sentModels[0].SequenceNumber, Is.EqualTo(1));

            var totalSent = sentModels.Sum(m => m.Data.Length);
            Assert.That(totalSent, Is.EqualTo(testData.Length));
        }

        [Test]
        public async Task SendRequests_CallsOnError_WhenTransmittedNotMatchExpected() {
            var testData = new byte[] { 1, 2, 3 };
            var errorCalled = false;
            long errorSeq = 0;
            int errorExpected = 0;
            int errorActual = 0;

            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(acceptedSocket);

            using var cts = new CancellationTokenSource();

            var pipelineTask = Task.Run(async () => {
                var readTask = pipeline.ReadFromSocket(null, cts.Token);
                var processTask = pipeline.ProcessPipe(8080, 12345, cts.Token);
                var sendTask = pipeline.SendRequests(
                    (model, ct) => Task.FromResult(1), // Return less than expected
                    (seq, transmitted) => { },
                    (seq, expected, actual) => {
                        errorCalled = true;
                        errorSeq = seq;
                        errorExpected = expected;
                        errorActual = actual;
                    },
                    cts.Token
                );

                await Task.WhenAll(readTask, processTask, sendTask);
            });

            await clientSocket.SendAsync(testData, SocketFlags.None);
            await Task.Delay(100);
            clientSocket.Shutdown(SocketShutdown.Both);

            await pipelineTask;

            Assert.That(errorCalled, Is.True);
            Assert.That(errorSeq, Is.EqualTo(1));
            Assert.That(errorExpected, Is.EqualTo(3));
            Assert.That(errorActual, Is.EqualTo(1));
        }

        [Test]
        public async Task Pipeline_AssignsSequenceNumbers_Sequentially() {
            var testData = new byte[1024];
            Array.Fill(testData, (byte)0xFF);
            var sequenceNumbers = new List<long>();

            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(
                acceptedSocket,
                maxInflight: 4,
                channelCapacity: 16,
                pauseThreshold: 256,
                resumeThreshold: 128
            );

            using var cts = new CancellationTokenSource();

            var pipelineTask = Task.Run(async () => {
                var readTask = pipeline.ReadFromSocket(null, cts.Token);
                var processTask = pipeline.ProcessPipe(8080, 12345, cts.Token);
                var sendTask = pipeline.SendRequests(
                    (model, ct) => {
                        lock(sequenceNumbers) {
                            sequenceNumbers.Add(model.SequenceNumber);
                        }
                        return Task.FromResult(model.Data.Length);
                    },
                    (seq, transmitted) => { },
                    (seq, expected, actual) => { },
                    cts.Token
                );

                await Task.WhenAll(readTask, processTask, sendTask);
            });

            await clientSocket.SendAsync(testData, SocketFlags.None);
            await Task.Delay(100);
            clientSocket.Shutdown(SocketShutdown.Both);

            await pipelineTask;

            Assert.That(sequenceNumbers.Count, Is.GreaterThan(0));
            for(int i = 0; i < sequenceNumbers.Count; i++) {
                Assert.That(sequenceNumbers[i], Is.EqualTo(i + 1));
            }
        }

        [Test]
        public async Task Pipeline_HandlesEmptyData_Gracefully() {
            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(acceptedSocket);

            using var cts = new CancellationTokenSource();
            var senderCalled = false;

            var pipelineTask = Task.Run(async () => {
                var readTask = pipeline.ReadFromSocket(null, cts.Token);
                var processTask = pipeline.ProcessPipe(8080, 12345, cts.Token);
                var sendTask = pipeline.SendRequests(
                    (model, ct) => {
                        senderCalled = true;
                        return Task.FromResult(model.Data.Length);
                    },
                    (seq, transmitted) => { },
                    (seq, expected, actual) => { },
                    cts.Token
                );

                await Task.WhenAll(readTask, processTask, sendTask);
            });

            clientSocket.Shutdown(SocketShutdown.Both);

            await pipelineTask;

            Assert.That(senderCalled, Is.False);
        }

        [Test]
        public async Task Pipeline_Cancellation_StopsProcessing() {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)0xAB);
            var chunksSent = 0;

            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(acceptedSocket);

            using var cts = new CancellationTokenSource();

            var pipelineTask = Task.Run(async () => {
                try {
                    var readTask = pipeline.ReadFromSocket(null, cts.Token);
                    var processTask = pipeline.ProcessPipe(8080, 12345, cts.Token);
                    var sendTask = pipeline.SendRequests(
                        async (model, ct) => {
                            await Task.Delay(10, ct);
                            Interlocked.Increment(ref chunksSent);
                            return model.Data.Length;
                        },
                        (seq, transmitted) => { },
                        (seq, expected, actual) => { },
                        cts.Token
                    );

                    await Task.WhenAll(readTask, processTask, sendTask);
                } catch(OperationCanceledException) {
                    // Expected when cancellation is requested
                }
            });

            await clientSocket.SendAsync(testData, SocketFlags.None);
            await Task.Delay(50);

            var chunksBefore = chunksSent;
            cts.Cancel();

            await pipelineTask;

            // Ensure cancellation stopped processing (no more chunks sent after cancel)
            await Task.Delay(50);
            Assert.That(chunksSent, Is.LessThanOrEqualTo(chunksBefore + 4)); // At most a few more from inflight
        }
    }
}
