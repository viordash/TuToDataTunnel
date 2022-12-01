using TuToProxy.Core.Exceptions;

namespace TuToProxy.Core.Queue {
    public class SortedQueue {
        Int64 frame;
        Int64 lastFrame;
        readonly int boundedCapacity;
        readonly object dataLock = new object();
        readonly Dictionary<Int64, byte[]?> storage;

        public bool IsEmpty {
            get {
                lock(dataLock) {
                    return !storage.Any();
                }
            }
        }

        public SortedQueue(Int64 startFrame, int boundedCapacity) {
            this.boundedCapacity = boundedCapacity;
            storage = new Dictionary<long, byte[]?>(boundedCapacity);
            frame = startFrame;
            lastFrame = startFrame;
        }

        public void Enqueue(Int64 frame, byte[]? data) {
            lock(dataLock) {
                if(storage.Count >= boundedCapacity) {
                    throw new TuToException($"sorted queue exceeds {boundedCapacity} limit");
                }
                if(!storage.TryAdd(frame, data)) {
                    throw new TuToException($"the same frame already exists");
                }
            }
        }

        public bool TryDequeue(out byte[]? result) {
            lock(dataLock) {
                if(storage.TryGetValue(frame, out result)) {
                    storage.Remove(frame);
                    lastFrame = frame;
                    frame++;
                    return true;
                }
                return false;
            }
        }

        public bool FrameWasTakenLast(Int64 frame) {
            lock(dataLock) {
                return lastFrame == frame;
            }
        }


    }
}
