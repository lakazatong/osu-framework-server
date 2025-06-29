﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osuTK.Input;

namespace osu.Framework.Input
{
    /// <summary>
    /// Manages state events for a single key.
    /// </summary>
    public class KeyEventManager : ButtonEventManager<Key>
    {
        public KeyEventManager(Key key)
            : base(key) { }

        public void HandleRepeat(InputState state)
        {
            // Only drawables that can still handle input should handle the repeat
            var drawables = ButtonDownInputQueue.AsNonNull().Intersect(InputQueue);

            PropagateButtonEvent(drawables, new KeyDownEvent(state, Button, true));
        }

        protected override Drawable? HandleButtonDown(InputState state, List<Drawable> targets) =>
            PropagateButtonEvent(targets, new KeyDownEvent(state, Button));

        protected override void HandleButtonUp(InputState state, List<Drawable> targets) =>
            PropagateButtonEvent(targets, new KeyUpEvent(state, Button));

        protected override bool SuppressLoggingEventInformation(Drawable drawable) =>
            drawable is ICanSuppressKeyEventLogging canSuppress
            && canSuppress.SuppressKeyEventLogging;
    }
}
