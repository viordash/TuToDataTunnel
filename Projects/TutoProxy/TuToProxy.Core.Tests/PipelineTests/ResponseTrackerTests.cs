using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TuToProxy.Core.Pipeline;

namespace TuToProxy.Core.Tests.PipelineTests {
    public class ResponseTrackerTests {

        [Test]
        public void TrackRequest_ReturnsIncompleteTask() {
            var tracker = new ResponseTracker();

            var task = tracker.TrackRequest(1);

            Assert.That(task.IsCompleted, Is.False);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void TrackRequest_MultipleRequests_AllTracked() {
            var tracker = new ResponseTracker();

            tracker.TrackRequest(1);
            tracker.TrackRequest(2);
            tracker.TrackRequest(3);

            Assert.That(tracker.PendingCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Complete_ResolvesTrackedTask() {
            var tracker = new ResponseTracker();
            var task = tracker.TrackRequest(1);

            var result = tracker.Complete(1, 100);

            Assert.That(result, Is.True);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(await task, Is.EqualTo(100));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Complete_UnknownSequenceNumber_ReturnsFalse() {
            var tracker = new ResponseTracker();
            tracker.TrackRequest(1);

            var result = tracker.Complete(999, 100);

            Assert.That(result, Is.False);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void Complete_AddsToTotalTransmitted() {
            var tracker = new ResponseTracker();
            tracker.TrackRequest(1);
            tracker.TrackRequest(2);

            tracker.Complete(1, 100);
            tracker.Complete(2, 200);

            Assert.That(tracker.TotalTransmitted, Is.EqualTo(300));
        }

        [Test]
        public void Fail_RejectsTrackedTask() {
            var tracker = new ResponseTracker();
            var task = tracker.TrackRequest(1);
            var exception = new InvalidOperationException("Test error");

            var result = tracker.Fail(1, exception);

            Assert.That(result, Is.True);
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception!.InnerException, Is.SameAs(exception));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Fail_UnknownSequenceNumber_ReturnsFalse() {
            var tracker = new ResponseTracker();
            tracker.TrackRequest(1);

            var result = tracker.Fail(999, new Exception("Test"));

            Assert.That(result, Is.False);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelAll_CancelsAllPendingTasks() {
            var tracker = new ResponseTracker();
            var task1 = tracker.TrackRequest(1);
            var task2 = tracker.TrackRequest(2);
            var task3 = tracker.TrackRequest(3);

            tracker.CancelAll();

            Assert.That(task1.IsCanceled, Is.True);
            Assert.That(task2.IsCanceled, Is.True);
            Assert.That(task3.IsCanceled, Is.True);
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void RecordReceived_AddsTotalReceived() {
            var tracker = new ResponseTracker();

            tracker.RecordReceived(100);
            tracker.RecordReceived(200);

            Assert.That(tracker.TotalReceived, Is.EqualTo(300));
        }

        [Test]
        public void RecordTransmitted_AddsTotalTransmitted() {
            var tracker = new ResponseTracker();

            tracker.RecordTransmitted(150);
            tracker.RecordTransmitted(250);

            Assert.That(tracker.TotalTransmitted, Is.EqualTo(400));
        }

        [Test]
        public void TotalTransmitted_CombinesRecordAndComplete() {
            var tracker = new ResponseTracker();
            tracker.RecordTransmitted(50);
            tracker.TrackRequest(1);

            tracker.Complete(1, 100);

            Assert.That(tracker.TotalTransmitted, Is.EqualTo(150));
        }

        [Test]
        public void InitialState_ZeroCounters() {
            var tracker = new ResponseTracker();

            Assert.That(tracker.TotalTransmitted, Is.EqualTo(0));
            Assert.That(tracker.TotalReceived, Is.EqualTo(0));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }
    }
}
