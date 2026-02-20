using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Channels;
using TuToProxy.Core.Models;
using TuToProxy.Core.Pooling;

namespace TuToProxy.Core.Pipeline;

public record TcpDataChunk(int Port, int OriginPort, ReadOnlyMemory<byte> Data, long SequenceNumber);

public class TcpPipeline<TModel> : IAsyncDisposable where TModel : DataBaseModel, new() {
    readonly Pipe pipe;
    readonly Channel<TcpDataChunk> sendChannel;
    readonly Socket socket;
    readonly int maxInflight;
    long sequenceNumber;

    public TcpPipeline(Socket socket, int maxInflight, int channelCapacity, int pauseThreshold, int resumeThreshold) {
        this.socket = socket;
        this.maxInflight = maxInflight;

        pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: pauseThreshold,
            resumeWriterThreshold: resumeThreshold,
            useSynchronizationContext: false
        ));

        sendChannel = Channel.CreateBounded<TcpDataChunk>(new BoundedChannelOptions(channelCapacity) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });
    }

    public TcpPipeline(Socket socket)
        : this(socket,
              TcpSocketParams.MaxInflightRequests,
              TcpSocketParams.ChannelCapacity,
              TcpSocketParams.PipePauseThreshold,
              TcpSocketParams.PipeResumeThreshold) {
    }

    public async ValueTask DisposeAsync() {
        sendChannel.Writer.TryComplete();
        await pipe.Reader.CompleteAsync();
        await pipe.Writer.CompleteAsync();
    }

    public async Task ReadFromSocket(Action<int>? onBytesReceived, CancellationToken ct) {
        var writer = pipe.Writer;
        try {
            while(!ct.IsCancellationRequested && socket.Connected) {
                var memory = writer.GetMemory(TcpSocketParams.ReceiveBufferSize);
                var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, ct);

                if(bytesRead == 0) {
                    break;
                }

                onBytesReceived?.Invoke(bytesRead);
                writer.Advance(bytesRead);

                var result = await writer.FlushAsync(ct);
                if(result.IsCompleted || result.IsCanceled) {
                    break;
                }
            }
        } finally {
            await writer.CompleteAsync();
        }
    }

    public async Task ProcessPipe(int port, int originPort, CancellationToken ct) {
        var reader = pipe.Reader;
        try {
            while(!ct.IsCancellationRequested) {
                var result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                if(buffer.IsEmpty && result.IsCompleted) {
                    break;
                }

                foreach(var segment in buffer) {
                    if(segment.Length > 0) {
                        var seq = Interlocked.Increment(ref sequenceNumber);
                        var chunk = new TcpDataChunk(port, originPort, segment.ToArray(), seq);
                        await sendChannel.Writer.WriteAsync(chunk, ct);
                    }
                }

                reader.AdvanceTo(buffer.End);

                if(result.IsCompleted) {
                    break;
                }
            }
        } finally {
            sendChannel.Writer.TryComplete();
            await reader.CompleteAsync();
        }
    }

    public async Task SendRequests(
        Func<TModel, CancellationToken, Task<int>> sender,
        Action<long, int> onResponse,
        Action<long, int, int> onError,
        CancellationToken ct) {

        var semaphore = new SemaphoreSlim(maxInflight, maxInflight);
        var tasks = new List<Task>();

        try {
            await foreach(var chunk in sendChannel.Reader.ReadAllAsync(ct)) {
                await semaphore.WaitAsync(ct);

                var task = ProcessChunk(chunk, sender, onResponse, onError, semaphore, ct);
                tasks.Add(task);

                // Clean up completed tasks
                tasks.RemoveAll(t => t.IsCompleted);
            }

            await Task.WhenAll(tasks);
        } catch(OperationCanceledException) {
            // Normal cancellation
        }
    }

    async Task ProcessChunk(
        TcpDataChunk chunk,
        Func<TModel, CancellationToken, Task<int>> sender,
        Action<long, int> onResponse,
        Action<long, int, int> onError,
        SemaphoreSlim semaphore,
        CancellationToken ct) {

        try {
            var model = DataModelPool<TModel>.Rent();
            model.Port = chunk.Port;
            model.OriginPort = chunk.OriginPort;
            model.Data = chunk.Data;
            model.SequenceNumber = chunk.SequenceNumber;

            try {
                var transmitted = await sender(model, ct);
                if(transmitted == chunk.Data.Length) {
                    onResponse(chunk.SequenceNumber, transmitted);
                } else {
                    onError(chunk.SequenceNumber, chunk.Data.Length, transmitted);
                }
            } finally {
                DataModelPool<TModel>.Return(model);
            }
        } finally {
            semaphore.Release();
        }
    }
}
