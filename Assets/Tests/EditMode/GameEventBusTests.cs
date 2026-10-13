using System;
using System.Collections.Generic;
using NUnit.Framework;
using WarOfNyx.Core.Events;

namespace WarOfNyx.Tests
{
    public class GameEventBusTests
    {
        private readonly struct Ping
        {
            public readonly int Value;
            public Ping(int value) => Value = value;
        }

        private readonly struct Pong
        {
        }

        [Test]
        public void DeliversOnlyToSubscribersOfThatType()
        {
            var bus = new GameEventBus();
            int pings = 0, pongs = 0;
            bus.Subscribe<Ping>(_ => pings++);
            bus.Subscribe<Pong>(_ => pongs++);

            bus.Publish(new Ping(1));

            Assert.AreEqual(1, pings);
            Assert.AreEqual(0, pongs);
        }

        [Test]
        public void DisposingSubscriptionStopsDelivery()
        {
            var bus = new GameEventBus();
            int received = 0;
            var sub = bus.Subscribe<Ping>(_ => received++);

            bus.Publish(new Ping(1));
            sub.Dispose();
            sub.Dispose(); // idempotent
            bus.Publish(new Ping(2));

            Assert.AreEqual(1, received);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void ThrowingHandlerDoesNotBlockOthers()
        {
            var bus = new GameEventBus();
            var errors = new List<Exception>();
            bus.HandlerFailed += errors.Add;
            int received = 0;

            bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<Ping>(_ => received++);
            bus.Publish(new Ping(1));

            Assert.AreEqual(1, received);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void HandlersMayUnsubscribeDuringPublish()
        {
            var bus = new GameEventBus();
            int received = 0;
            IDisposable sub = null;
            sub = bus.Subscribe<Ping>(_ =>
            {
                received++;
                sub.Dispose();
            });

            bus.Publish(new Ping(1));
            bus.Publish(new Ping(2));

            Assert.AreEqual(1, received);
        }

        [Test]
        public void PayloadIsDelivered()
        {
            var bus = new GameEventBus();
            int value = 0;
            bus.Subscribe<Ping>(p => value = p.Value);
            bus.Publish(new Ping(42));
            Assert.AreEqual(42, value);
        }

        [Test]
        public void PublishWithoutSubscribersIsSafe()
        {
            Assert.DoesNotThrow(() => new GameEventBus().Publish(new Pong()));
        }
    }
}
