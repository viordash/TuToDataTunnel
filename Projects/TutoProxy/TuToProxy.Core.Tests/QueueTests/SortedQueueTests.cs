using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Queue;

namespace TuToProxy.Core.Tests.ModelsTests {
    public class SortedQueueTests {

        [Test]
        public void Limit_Exceeds_Throws_Exception() {
            var testable = new SortedQueue(0, 10);

            for(int i = 0; i < 10; i++) {
                testable.Enqueue(i, new byte[] { (byte)i });
            }
            Assert.Throws<TuToException>(() => testable.Enqueue(10, new byte[] { 10 }));
        }

        [Test]
        public void Enqueue_Repeating_Frame_Throws_Exception() {
            var testable = new SortedQueue(0, 10);

            testable.Enqueue(0, new byte[] { 0 });
            Assert.Throws<TuToException>(() => testable.Enqueue(0, new byte[] { 1 }));
        }

        [Test]
        public async Task MultiThread_Enqueue_Test() {
            var testable = new SortedQueue(0, 1000);

            var tasks = Enumerable.Range(0, 1000).Select(x => Task.Run(() => testable.Enqueue(x, new byte[] { (byte)x })));

            await Task.WhenAll(tasks.ToArray());

            for(int i = 0; i < 1000; i++) {
                Assert.That(testable.TryDequeue(out byte[]? data), Is.True);
                Assert.That(data?[0], Is.EqualTo((byte)i));
            }
        }

        [Test]
        public void TryDequeue_Return_Data_In_Sorted_Order_Test() {
            var testable = new SortedQueue(0, 1000);

            testable.Enqueue(3, new byte[] { 3 });
            testable.Enqueue(0, new byte[] { 0 });
            testable.Enqueue(2, new byte[] { 2 });
            testable.Enqueue(1, new byte[] { 1 });

            byte[]? data;
            Assert.That(testable.TryDequeue(out data), Is.True);
            Assert.That(data?[0], Is.EqualTo(0));

            Assert.That(testable.TryDequeue(out data), Is.True);
            Assert.That(data?[0], Is.EqualTo(1));

            Assert.That(testable.TryDequeue(out data), Is.True);
            Assert.That(data?[0], Is.EqualTo(2));

            Assert.That(testable.TryDequeue(out data), Is.True);
            Assert.That(data?[0], Is.EqualTo(3));
        }

        [Test]
        public void TryDequeue_No_Data_Test() {
            var testable = new SortedQueue(10, 1000);

            byte[]? data;
            Assert.That(testable.TryDequeue(out data), Is.False);

            testable.Enqueue(3, new byte[] { 3 });
            Assert.That(testable.TryDequeue(out data), Is.False);

            testable.Enqueue(10, new byte[] { 10 });
            Assert.That(testable.TryDequeue(out data), Is.True);
            Assert.That(data?[0], Is.EqualTo(10));
        }

        [Test]
        public void TryDequeue_Remove_Returned_From_Queue_Test() {
            var testable = new SortedQueue(0, 10);

            for(int i = 0; i < 10; i++) {
                testable.Enqueue(i, new byte[] { (byte)i });
            }
            Assert.Throws<TuToException>(() => testable.Enqueue(10, new byte[] { 10 }));

            Assert.That(testable.TryDequeue(out _), Is.True);

            testable.Enqueue(10, new byte[] { 10 });
        }

        [Test]
        public void IsEmpty_Test() {
            var testable = new SortedQueue(0, 10);

            Assert.That(testable.IsEmpty, Is.True);

            testable.Enqueue(0, new byte[] { 0 });
            Assert.That(testable.IsEmpty, Is.False);

            testable.TryDequeue(out _);
            Assert.That(testable.IsEmpty, Is.True);
        }

        [Test]
        public void FrameWasTakenLast_Test() {
            var testable = new SortedQueue(0, 10);

            testable.Enqueue(0, new byte[] { 0 });
            Assert.That(testable.TryDequeue(out _), Is.True);
            Assert.That(testable.FrameWasTakenLast(0), Is.True);
            Assert.That(testable.FrameWasTakenLast(1), Is.False);
            Assert.That(testable.FrameWasTakenLast(2), Is.False);

            testable.Enqueue(1, new byte[] { 1 });
            Assert.That(testable.TryDequeue(out _), Is.True);
            Assert.That(testable.FrameWasTakenLast(0), Is.False);
            Assert.That(testable.FrameWasTakenLast(1), Is.True);
            Assert.That(testable.FrameWasTakenLast(2), Is.False);
        }
    }
}
