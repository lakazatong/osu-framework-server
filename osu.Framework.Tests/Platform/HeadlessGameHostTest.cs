﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Testing;

namespace osu.Framework.Tests.Platform
{
    [TestFixture]
    public partial class HeadlessGameHostTest
    {
        [Test]
        public void TestGameHostExceptionDuringSetupHost()
        {
            using (
                var host = new ExceptionDuringSetupGameHost(
                    nameof(TestGameHostExceptionDuringSetupHost)
                )
            )
            {
                Assert.Throws<InvalidOperationException>(() => host.Run(new TestGame()));

                Assert.AreEqual(ExecutionState.Idle, host.ExecutionState);
            }
        }

        /// <summary>
        /// <see cref="GameHost.PerformExit"/> is virtual, but there are some cases where exit is mandatory.
        /// This aims to test that shutdown from an exception firing (ie. the `finally` portion of <see cref="GameHost.Run"/>)
        /// fires correctly even if the base call of <see cref="GameHost.PerformExit"/> is omitted.
        /// </summary>
        [Test]
        public void TestGameHostExceptionDuringAsynchronousChildLoad()
        {
            using (
                var host = new TestRunHeadlessGameHostWithOverriddenExit(
                    nameof(TestGameHostExceptionDuringAsynchronousChildLoad)
                )
            )
            {
                Assert.Throws<InvalidOperationException>(() =>
                    host.Run(new ExceptionDuringAsynchronousLoadTestGame())
                );

                Assert.AreEqual(ExecutionState.Stopped, host.ExecutionState);
            }
        }

        [Test]
        public void TestGameHostDisposalWhenNeverRun()
        {
            using (
                new TestRunHeadlessGameHost(
                    nameof(TestGameHostDisposalWhenNeverRun),
                    new HostOptions(),
                    true
                )
            )
            {
                // never call host.Run()
            }
        }

        [Test]
        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        public void TestThreadSafetyResetOnEnteringThread()
        {
            using (
                var host = new TestRunHeadlessGameHost(
                    nameof(TestThreadSafetyResetOnEnteringThread),
                    new HostOptions()
                )
            )
            {
                bool isDrawThread = false;
                bool isUpdateThread = false;
                bool isInputThread = false;
                bool isAudioThread = false;

                var task = Task.Factory.StartNew(
                    () =>
                    {
                        var game = new TestGame();
                        game.Scheduler.Add(() => host.Exit());

                        host.Run(game);

                        isDrawThread = ThreadSafety.IsDrawThread;
                        isUpdateThread = ThreadSafety.IsUpdateThread;
                        isInputThread = ThreadSafety.IsInputThread;
                        isAudioThread = ThreadSafety.IsAudioThread;
                    },
                    TaskCreationOptions.LongRunning
                );

                task.WaitSafely();

                Assert.That(!isDrawThread && !isUpdateThread && !isInputThread && !isAudioThread);
            }
        }

        public class ExceptionDuringSetupGameHost : TestRunHeadlessGameHost
        {
            public ExceptionDuringSetupGameHost(string gameName)
                : base(gameName, new HostOptions()) { }

            protected override void SetupForRun()
            {
                base.SetupForRun();
                throw new InvalidOperationException();
            }
        }

        public class TestRunHeadlessGameHostWithOverriddenExit : TestRunHeadlessGameHost
        {
            public TestRunHeadlessGameHostWithOverriddenExit(string gameName)
                : base(gameName, new HostOptions()) { }

            protected override void PerformExit(bool immediately)
            {
                // matches TestGameHost behaviour for testing purposes
            }
        }

        internal partial class ExceptionDuringAsynchronousLoadTestGame : TestGame
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                throw new InvalidOperationException();
            }
        }
    }
}
