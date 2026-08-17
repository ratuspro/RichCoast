using System;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>
    /// The seam's dispatch rules. New for the Unity port (the original used Phaser's emitter), so
    /// these pin the behaviour the zones rely on: delivery order, safe mutation during dispatch,
    /// and no allocation per emit.
    /// </summary>
    public class EventBusTests
    {
        [Test]
        public void DeliversToEverySubscriberInSubscriptionOrder()
        {
            var bus = new EventBus();
            var order = "";
            bus.Subscribe<ScoreChanged>(_ => order += "a");
            bus.Subscribe<ScoreChanged>(_ => order += "b");

            bus.Emit(new ScoreChanged(10));

            Assert.That(order, Is.EqualTo("ab"));
        }

        [Test]
        public void CarriesThePayloadUnchanged()
        {
            var bus = new EventBus();
            var seen = default(BallDropped);
            bus.Subscribe<BallDropped>(e => seen = e);

            bus.Emit(new BallDropped(BallSpec.FromTier(4), 123f));

            Assert.That(seen.Ball.Tier, Is.EqualTo(4));
            Assert.That(seen.Ball.Value, Is.EqualTo(27));
            Assert.That(seen.X, Is.EqualTo(123f));
        }

        [Test]
        public void EmittingWithNoSubscribersIsNotAnError() =>
            Assert.DoesNotThrow(() => new EventBus().Emit(new ZoneBEmpty()));

        [Test]
        public void KeepsEventTypesIndependent()
        {
            var bus = new EventBus();
            var busy = 0;
            bus.Subscribe<ZoneBBusy>(_ => busy++);

            bus.Emit(new ZoneBEmpty());

            Assert.That(busy, Is.EqualTo(0));
        }

        [Test]
        public void UnsubscribeStopsFurtherDelivery()
        {
            var bus = new EventBus();
            var hits = 0;
            Action<ZoneBEmpty> handler = _ => hits++;
            bus.Subscribe(handler);

            bus.Emit(new ZoneBEmpty());
            bus.Unsubscribe(handler);
            bus.Emit(new ZoneBEmpty());

            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void SubscribingDuringADispatchDoesNotAffectTheCurrentEmit()
        {
            var bus = new EventBus();
            var late = 0;
            bus.Subscribe<ZoneBEmpty>(_ => bus.Subscribe<ZoneBEmpty>(__ => late++));

            bus.Emit(new ZoneBEmpty()); // registers the late handler, must not call it
            Assert.That(late, Is.EqualTo(0));

            bus.Emit(new ZoneBEmpty());
            Assert.That(late, Is.EqualTo(1));
        }

        [Test]
        public void UnsubscribingDuringADispatchTakesEffectAfterIt()
        {
            var bus = new EventBus();
            var hits = 0;
            Action<ZoneBEmpty> second = _ => hits++;
            bus.Subscribe<ZoneBEmpty>(_ => bus.Unsubscribe(second));
            bus.Subscribe(second);

            bus.Emit(new ZoneBEmpty()); // every subscriber sees a consistent snapshot
            Assert.That(hits, Is.EqualTo(1));

            bus.Emit(new ZoneBEmpty());
            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void EmitAllocatesNothing()
        {
            // The gameplay loop's allocation budget on the target hardware is zero: a GC spike on
            // a Snapdragon 4xx is a visible hitch.
            var bus = new EventBus();
            var total = 0d;
            bus.Subscribe<ScoreChanged>(e => total += e.Total);
            bus.Emit(new ScoreChanged(1)); // warm up the channel and the delegate

            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < 1000; i++) bus.Emit(new ScoreChanged(1));
            var after = GC.GetTotalMemory(false);

            Assert.That(after - before, Is.LessThanOrEqualTo(0), "Emit allocated on the managed heap");
            Assert.That(total, Is.EqualTo(1001));
        }

        [Test]
        public void ClearDropsEverySubscription()
        {
            var bus = new EventBus();
            bus.Subscribe<ZoneBEmpty>(_ => Assert.Fail("cleared subscriber was called"));
            bus.Clear();
            bus.Emit(new ZoneBEmpty());
            Assert.That(bus.SubscriberCount<ZoneBEmpty>(), Is.EqualTo(0));
        }
    }
}
