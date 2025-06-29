﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using NUnit.Framework;
using osu.Framework.Audio;

namespace osu.Framework.Tests.Audio
{
    [TestFixture]
    public class AudioCollectionManagerTest
    {
        [Test]
        public void TestDisposalWhileItemsAreAddedDoesNotThrowInvalidOperationException()
        {
            var manager = new TestAudioCollectionManager();

            var updateLoopStarted = new ManualResetEventSlim();

            // add a huge amount of items to the queue
            for (int i = 0; i < 10000; i++)
                manager.AddItem(new TestingAdjustableAudioComponent());

            // in a separate thread start processing the queue
            var thread = AudioTestHelper.StartNewAudioThread(() =>
            {
                while (!manager.IsDisposed)
                {
                    updateLoopStarted.Set();
                    manager.Update();
                }
            });

            Assert.IsTrue(updateLoopStarted.Wait(10000));

            Assert.DoesNotThrow(() => manager.Dispose());

            thread.Dispose();
        }

        private class TestAudioCollectionManager : AudioCollectionManager<AdjustableAudioComponent>
        {
            public new bool IsDisposed => base.IsDisposed;
        }

        private class TestingAdjustableAudioComponent : AdjustableAudioComponent { }
    }
}
