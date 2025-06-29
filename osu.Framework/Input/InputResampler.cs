﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Framework.Input
{
    /// <summary>
    /// Reduces cursor input to relevant nodes and corners that noticably affect the cursor path.
    /// If the input is a raw/HD input this won't omit any input nodes.
    /// Set SmoothRawInput to true to keep behaviour for HD inputs.
    /// </summary>
    public class InputResampler
    {
        private Vector2? lastRelevantPosition;

        private Vector2? lastActualPosition;

        private bool isRawInput;

        /// <summary>
        /// true if AddPosition should treat raw input (input with a decimal fraction) the same
        /// as normal input. If false, AddPosition will always just return the position argument
        /// passed to the function without modification.
        /// </summary>
        public bool ResampleRawInput { get; set; }

        private readonly List<Vector2> returnedPositions = new List<Vector2>();

        /// <summary>
        /// Function that takes in a <paramref name="position"/> and returns a list of positions
        /// that can be used by the caller to make the input path smoother or reduce it.
        /// The current implementation always returns only none or exactly one vector which
        /// reduces the input to the corner nodes.
        /// </summary>
        /// <remarks>
        /// To save on allocations, the returned list is only valid until the next call of <see cref="AddPosition"/>.
        /// </remarks>
        public List<Vector2> AddPosition(Vector2 position)
        {
            returnedPositions.Clear();

            if (!ResampleRawInput)
            {
                if (isRawInput)
                {
                    lastRelevantPosition = position;
                    lastActualPosition = position;

                    returnedPositions.Add(position);
                    return returnedPositions;
                }

                // HD if it has fractions
                if (position.X - MathF.Truncate(position.X) != 0)
                    isRawInput = true;
            }

            if (lastRelevantPosition == null || lastActualPosition == null)
            {
                lastRelevantPosition = position;
                lastActualPosition = position;

                returnedPositions.Add(position);
                return returnedPositions;
            }

            Vector2 diff = position - lastRelevantPosition.Value;
            float distance = diff.Length;
            Vector2 direction = diff / distance;

            Vector2 realDiff = position - lastActualPosition.Value;
            float realMovementDistance = realDiff.Length;
            if (realMovementDistance < 1)
                return returnedPositions;

            lastActualPosition = position;

            // don't update when it moved less than 10 pixels from the last position in a straight fashion
            // but never update when its less than 2 pixels
            if (
                (distance < 10 && Vector2.Dot(direction, realDiff / realMovementDistance) > 0.7)
                || distance < 2
            )
                return returnedPositions;

            lastRelevantPosition = position;

            returnedPositions.Add(position);
            return returnedPositions;
        }
    }
}
