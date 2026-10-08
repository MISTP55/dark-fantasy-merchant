using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NotificationFeedTests
    {
        private const float DisplaySeconds = 5f;
        private const float FadeSeconds = 1f;
        private const float Tolerance = 1e-4f;

        private static NotificationFeed CreateFeed()
        {
            return new NotificationFeed(DisplaySeconds, FadeSeconds);
        }

        [Test]
        public void NewFeed_IsEmpty()
        {
            Assert.AreEqual(0, CreateFeed().Notifications.Count);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_RejectsAnInvalidDisplayTime(float displaySeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationFeed(displaySeconds, FadeSeconds));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_RejectsAnInvalidFadeTime(float fadeSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationFeed(DisplaySeconds, fadeSeconds));
        }

        [Test]
        public void Post_AddsAnOpaqueNotificationWithItsText()
        {
            NotificationFeed feed = CreateFeed();

            Notification notification = feed.Post("Arrived");

            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreSame(notification, feed.Notifications[0]);
            Assert.AreEqual("Arrived", notification.Text);
            Assert.AreEqual(1f, notification.Opacity);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Post_RejectsAnEmptyText(string text)
        {
            NotificationFeed feed = CreateFeed();

            Assert.Throws<ArgumentException>(() => feed.Post(text));
            Assert.AreEqual(0, feed.Notifications.Count);
        }

        [Test]
        public void Post_PutsTheNewestFirst()
        {
            NotificationFeed feed = CreateFeed();

            Notification first = feed.Post("First");
            Notification second = feed.Post("Second");
            Notification third = feed.Post("Third");

            Assert.AreSame(third, feed.Notifications[0]);
            Assert.AreSame(second, feed.Notifications[1]);
            Assert.AreSame(first, feed.Notifications[2]);
        }

        [Test]
        public void Post_KeepsTwoNotificationsWithTheSameText()
        {
            NotificationFeed feed = CreateFeed();

            feed.Post("Same");
            feed.Post("Same");

            Assert.AreEqual(2, feed.Notifications.Count);
        }

        [Test]
        public void Post_RaisesPosted()
        {
            NotificationFeed feed = CreateFeed();
            var posted = new List<Notification>();
            feed.Posted += posted.Add;

            Notification notification = feed.Post("Arrived");

            Assert.AreEqual(1, posted.Count);
            Assert.AreSame(notification, posted[0]);
        }

        [Test]
        public void Notification_StaysOpaque_ForTheDisplayTime()
        {
            NotificationFeed feed = CreateFeed();
            Notification notification = feed.Post("Arrived");

            feed.Advance(DisplaySeconds);

            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreEqual(1f, notification.Opacity);
        }

        [Test]
        public void Notification_FadesOut_AfterTheDisplayTime()
        {
            NotificationFeed feed = CreateFeed();
            Notification notification = feed.Post("Arrived");

            feed.Advance(DisplaySeconds + FadeSeconds * 0.25f);

            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreEqual(0.75f, notification.Opacity, Tolerance);
        }

        [Test]
        public void Notification_IsRemoved_AfterTheFade()
        {
            NotificationFeed feed = CreateFeed();
            Notification notification = feed.Post("Arrived");
            var removed = new List<Notification>();
            feed.Removed += removed.Add;

            feed.Advance(DisplaySeconds);
            feed.Advance(FadeSeconds);

            Assert.AreEqual(0, feed.Notifications.Count);
            Assert.AreEqual(1, removed.Count);
            Assert.AreSame(notification, removed[0]);
            Assert.AreEqual(0f, notification.Opacity);
        }

        [Test]
        public void Notification_IsRemovedAtTheEndOfTheDisplayTime_WithoutAFade()
        {
            var feed = new NotificationFeed(DisplaySeconds, 0f);
            Notification notification = feed.Post("Arrived");

            feed.Advance(DisplaySeconds * 0.5f);

            Assert.AreEqual(1f, notification.Opacity);

            feed.Advance(DisplaySeconds * 0.5f);

            Assert.AreEqual(0, feed.Notifications.Count);
        }

        [Test]
        public void Notifications_AgeSeparately()
        {
            NotificationFeed feed = CreateFeed();
            Notification older = feed.Post("Older");

            feed.Advance(3f);

            Notification newer = feed.Post("Newer");

            feed.Advance(3f);

            // The older one is a second into its fade, which removes it; the newer one has two seconds left.
            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreSame(newer, feed.Notifications[0]);
            Assert.AreEqual(1f, newer.Opacity);
            Assert.AreEqual(0f, older.Opacity);
        }

        [Test]
        public void Advance_RemovesTheOldestFirst_InOneStep()
        {
            NotificationFeed feed = CreateFeed();
            Notification older = feed.Post("Older");
            feed.Advance(1f);
            Notification newer = feed.Post("Newer");
            var removed = new List<Notification>();
            feed.Removed += removed.Add;

            feed.Advance(60f);

            Assert.AreEqual(0, feed.Notifications.Count);
            CollectionAssert.AreEqual(new[] { older, newer }, removed);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Advance_IgnoresAStepThatIsNotPositive(float seconds)
        {
            NotificationFeed feed = CreateFeed();
            Notification notification = feed.Post("Arrived");
            feed.Advance(DisplaySeconds + FadeSeconds * 0.5f);

            feed.Advance(seconds);

            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreEqual(0.5f, notification.Opacity, Tolerance);
        }

        [Test]
        public void ANotificationPostedWhenAnotherIsRemoved_IsKept()
        {
            NotificationFeed feed = CreateFeed();
            feed.Post("Older");
            Notification followUp = null;
            feed.Removed += _ => followUp = feed.Post("Follow-up");

            feed.Advance(60f);

            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreSame(followUp, feed.Notifications[0]);
            Assert.AreEqual(1f, followUp.Opacity);
        }
    }
}
