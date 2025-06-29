// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Platform.Apple.Native;
using osu.Framework.Platform.SDL2;
using osuTK;
using Selector = osu.Framework.Platform.Apple.Native.Selector;

namespace osu.Framework.Platform.MacOS
{
    /// <summary>
    /// macOS-specific subclass of <see cref="SDL2Window"/>.
    /// </summary>
    internal class SDL2MacOSWindow : SDL2DesktopWindow
    {
        private static readonly IntPtr sel_hasprecisescrollingdeltas = Selector.Get(
            "hasPreciseScrollingDeltas"
        );
        private static readonly IntPtr sel_scrollingdeltax = Selector.Get("scrollingDeltaX");
        private static readonly IntPtr sel_scrollingdeltay = Selector.Get("scrollingDeltaY");
        private static readonly IntPtr sel_respondstoselector_ = Selector.Get(
            "respondsToSelector:"
        );

        private delegate void ScrollWheelDelegate(IntPtr handle, IntPtr selector, IntPtr theEvent); // v@:@

        private IntPtr originalScrollWheel;
        private ScrollWheelDelegate scrollWheelHandler;

        public SDL2MacOSWindow(GraphicsSurfaceType surfaceType, string appName)
            : base(surfaceType, appName) { }

        public override void Create()
        {
            base.Create();

            // replace [SDLView scrollWheel:(NSEvent *)] with our own version
            IntPtr viewClass = Class.Get("SDLView");
            scrollWheelHandler = scrollWheel;
            originalScrollWheel = Class.SwizzleMethod(
                viewClass,
                "scrollWheel:",
                "v@:@",
                scrollWheelHandler
            );
        }

        /// <summary>
        /// Swizzled replacement of [SDLView scrollWheel:(NSEvent *)] that checks for precise scrolling deltas.
        /// </summary>
        private void scrollWheel(IntPtr receiver, IntPtr selector, IntPtr theEvent)
        {
            bool hasPrecise =
                Interop.SendBool(theEvent, sel_respondstoselector_, sel_hasprecisescrollingdeltas)
                && Interop.SendBool(theEvent, sel_hasprecisescrollingdeltas);

            if (!hasPrecise)
            {
                // calls the unswizzled [SDLView scrollWheel:(NSEvent *)] method if this is a regular scroll wheel event
                // the receiver may sometimes not be SDLView, ensure it has a scroll wheel selector implemented before attempting to call.
                if (Interop.SendBool(receiver, sel_respondstoselector_, originalScrollWheel))
                    Interop.SendVoid(receiver, originalScrollWheel, theEvent);

                return;
            }

            // according to osuTK, 0.1f is the scaling factor expected to be returned by CGEventSourceGetPixelsPerLine
            // this is additionally scaled down by a factor of 8 so that a precise scroll of 1.0 is roughly equivalent to one notch on a traditional scroll wheel.
            const float scale_factor = 0.1f / 8;

            float scrollingDeltaX = Interop.SendFloat(theEvent, sel_scrollingdeltax);
            float scrollingDeltaY = Interop.SendFloat(theEvent, sel_scrollingdeltay);

            ScheduleEvent(() =>
                TriggerMouseWheel(
                    new Vector2(scrollingDeltaX * scale_factor, scrollingDeltaY * scale_factor),
                    true
                )
            );
        }
    }
}
