﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using TKKey = osuTK.Input.Key;

namespace osu.Framework.Input.Handlers.Keyboard
{
    public class KeyboardHandler : InputHandler
    {
        private static readonly GlobalStatistic<ulong> statistic_total_events =
            GlobalStatistics.Get<ulong>(StatisticGroupFor<KeyboardHandler>(), "Total events");

        public override string Description => "Keyboard";

        public override bool IsActive => true;

        public override bool Initialize(GameHost host)
        {
            if (!base.Initialize(host))
                return false;

            if (!(host.Window is ISDLWindow window))
                return false;

            Enabled.BindValueChanged(
                e =>
                {
                    if (e.NewValue)
                    {
                        window.KeyDown += handleKeyDown;
                        window.KeyUp += handleKeyUp;
                    }
                    else
                    {
                        window.KeyDown -= handleKeyDown;
                        window.KeyUp -= handleKeyUp;
                    }
                },
                true
            );

            return true;
        }

        private void enqueueInput(IInput input)
        {
            PendingInputs.Enqueue(input);
            FrameStatistics.Increment(StatisticsCounterType.KeyEvents);
            statistic_total_events.Value++;
        }

        private void handleKeyDown(TKKey key) => enqueueInput(new KeyboardKeyInput(key, true));

        private void handleKeyUp(TKKey key) => enqueueInput(new KeyboardKeyInput(key, false));
    }
}
