﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Extensions.TypeExtensions;

namespace osu.Framework.Timing
{
    public class StopwatchClock : Stopwatch, IAdjustableClock
    {
        private double seekOffset;

        /// <summary>
        /// Keep track of how much stopwatch time we have used at previous rates.
        /// </summary>
        private double rateChangeUsed;

        /// <summary>
        /// Keep track of the resultant time that was accumulated at previous rates.
        /// </summary>
        private double rateChangeAccumulated;

        public StopwatchClock(bool start = false)
        {
            if (start)
                Start();
        }

        public virtual double CurrentTime => stopwatchCurrentTime + seekOffset;

        /// <summary>
        /// The current time, represented solely by the accumulated <see cref="Stopwatch"/> time.
        /// </summary>
        private double stopwatchCurrentTime =>
            (stopwatchMilliseconds - rateChangeUsed) * rate + rateChangeAccumulated;

        private double stopwatchMilliseconds => (double)ElapsedTicks / Frequency * 1000;

        private double rate = 1;

        public double Rate
        {
            get => rate;
            set
            {
                if (rate == value)
                    return;

                rateChangeAccumulated += (stopwatchMilliseconds - rateChangeUsed) * rate;
                rateChangeUsed = stopwatchMilliseconds;

                rate = value;
            }
        }

        public new void Reset()
        {
            resetAccumulatedRate();
            base.Reset();
        }

        public new void Restart()
        {
            resetAccumulatedRate();
            base.Restart();
        }

        public void ResetSpeedAdjustments() => Rate = 1;

        public virtual bool Seek(double position)
        {
            // Determine the offset that when added to stopwatchCurrentTime; results in the requested time value
            seekOffset = position - stopwatchCurrentTime;
            return true;
        }

        public override string ToString() =>
            $@"{GetType().ReadableName()} ({Math.Truncate(CurrentTime)}ms)";

        private void resetAccumulatedRate()
        {
            rateChangeAccumulated = 0;
            rateChangeUsed = 0;
        }
    }
}
