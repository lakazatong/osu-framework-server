// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using osu.Framework.Input;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Platform.SDL2;
using osu.Framework.Platform.Windows.Native;
using osuTK;
using osuTK.Input;
using static SDL2.SDL;
using Icon = osu.Framework.Platform.Windows.Native.Icon;

namespace osu.Framework.Platform.Windows
{
    [SupportedOSPlatform("windows")]
    internal class SDL2WindowsWindow : SDL2DesktopWindow, IWindowsWindow
    {
        private const int seticon_message = 0x0080;
        private const int icon_big = 1;
        private const int icon_small = 0;

        private const int large_icon_size = 256;
        private const int small_icon_size = 16;

        private Icon? smallIcon;
        private Icon? largeIcon;

        private const int wm_killfocus = 8;

        /// <summary>
        /// Whether to apply the <see cref="windows_borderless_width_hack"/>.
        /// </summary>
        private readonly bool applyBorderlessWindowHack;

        public SDL2WindowsWindow(GraphicsSurfaceType surfaceType, string appName)
            : base(surfaceType, appName)
        {
            switch (surfaceType)
            {
                case GraphicsSurfaceType.OpenGL:
                case GraphicsSurfaceType.Vulkan:
                    applyBorderlessWindowHack = true;
                    break;

                case GraphicsSurfaceType.Direct3D11:
                    applyBorderlessWindowHack = false;
                    break;
            }

            if (!declareDpiAwareV2())
                declareDpiAware();
        }

        private bool declareDpiAwareV2()
        {
            try
            {
                return SetProcessDpiAwarenessContext(
                    DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                );
            }
            catch
            {
                return false;
            }
        }

        private bool declareDpiAware()
        {
            try
            {
                return SetProcessDpiAwareness(ProcessDpiAwareness.Process_Per_Monitor_DPI_Aware);
            }
            catch
            {
                return false;
            }
        }

        public override void Create()
        {
            base.Create();

            // disable all pen and touch feedback as this causes issues when running "optimised" fullscreen under Direct3D11.
            foreach (var feedbackType in Enum.GetValues<FeedbackType>())
                Native.Input.SetWindowFeedbackSetting(WindowHandle, feedbackType, false);

            // enable window message events to use with `OnSDLEvent` below.
            SDL_EventState(SDL_EventType.SDL_SYSWMEVENT, SDL_ENABLE);
        }

        protected override void HandleEventFromFilter(SDL_Event e)
        {
            if (e.type == SDL_EventType.SDL_SYSWMEVENT)
            {
                var wmMsg = Marshal.PtrToStructure<SDL2Structs.SDL_SysWMmsg>(e.syswm.msg);
                var m = wmMsg.msg.win;

                switch (m.msg)
                {
                    case wm_killfocus:
                        warpCursorFromFocusLoss();
                        break;

                    case Imm.WM_IME_STARTCOMPOSITION:
                    case Imm.WM_IME_COMPOSITION:
                    case Imm.WM_IME_ENDCOMPOSITION:
                        handleImeMessage(m.hwnd, m.msg, m.lParam);
                        break;
                }
            }

            base.HandleEventFromFilter(e);
        }

        public Vector2? LastMousePosition { get; set; }

        /// <summary>
        /// If required, warps the OS cursor to match the framework cursor position.
        /// </summary>
        /// <remarks>
        /// The normal warp in <see cref="MouseHandler.transferLastPositionToHostCursor"/> doesn't work in fullscreen,
        /// as it is called when the window has already lost focus and is minimized.
        /// So we do an out-of-band warp, immediately after receiving the <see cref="wm_killfocus"/> message.
        /// </remarks>
        private void warpCursorFromFocusLoss()
        {
            if (
                LastMousePosition.HasValue
                && WindowMode.Value == Configuration.WindowMode.Fullscreen
                && RelativeMouseMode
            )
            {
                var pt = PointToScreen(
                    new Point((int)LastMousePosition.Value.X, (int)LastMousePosition.Value.Y)
                );
                SDL_WarpMouseGlobal(pt.X, pt.Y); // this directly calls the SetCursorPos win32 API
            }
        }

        #region IME handling

        public override void StartTextInput(TextInputProperties properties)
        {
            base.StartTextInput(properties);
            ScheduleCommand(() =>
                Imm.SetImeAllowed(
                    WindowHandle,
                    properties.Type.SupportsIme() && properties.AllowIme
                )
            );
        }

        public override void ResetIme() =>
            ScheduleCommand(() => Imm.CancelComposition(WindowHandle));

        protected override unsafe void HandleTextInputEvent(SDL_TextInputEvent evtText)
        {
            if (!SDL2Extensions.TryGetStringFromBytePointer(evtText.text, out string sdlResult))
                return;

            // Block SDL text input if it was already handled by `handleImeMessage()`.
            // SDL truncates text over 32 bytes and sends it as multiple events.
            // We assume these events will be handled in the same `pollSDLEvents()` call.
            if (lastImeResult?.Contains(sdlResult) == true)
            {
                // clear the result after this SDL event loop finishes so normal text input isn't blocked.
                EventScheduler.AddOnce(() => lastImeResult = null);
                return;
            }

            // also block if there is an ongoing composition (unlikely to occur).
            if (imeCompositionActive)
                return;

            base.HandleTextInputEvent(evtText);
        }

        protected override void HandleTextEditingEvent(SDL_TextEditingEvent evtEdit)
        {
            // handled by custom logic below
        }

        /// <summary>
        /// Whether IME composition is active.
        /// </summary>
        /// <remarks>Used for blocking SDL IME results since we handle those ourselves.</remarks>
        private bool imeCompositionActive;

        /// <summary>
        /// The last IME result.
        /// </summary>
        /// <remarks>
        /// Used for blocking SDL IME results since we handle those ourselves.
        /// Cleared when the SDL events are blocked.
        /// </remarks>
        private string? lastImeResult;

        private void handleImeMessage(IntPtr hWnd, uint uMsg, long lParam)
        {
            switch (uMsg)
            {
                case Imm.WM_IME_STARTCOMPOSITION:
                    imeCompositionActive = true;
                    ScheduleEvent(() => TriggerTextEditing(string.Empty, 0, 0));
                    break;

                case Imm.WM_IME_COMPOSITION:
                    using (var inputContext = new Imm.InputContext(hWnd, lParam))
                    {
                        if (inputContext.TryGetImeResult(out string? resultText))
                        {
                            lastImeResult = resultText;
                            ScheduleEvent(() => TriggerTextInput(resultText));
                        }

                        if (
                            inputContext.TryGetImeComposition(
                                out string? compositionText,
                                out int start,
                                out int length
                            )
                        )
                        {
                            ScheduleEvent(() => TriggerTextEditing(compositionText, start, length));
                        }
                    }

                    break;

                case Imm.WM_IME_ENDCOMPOSITION:
                    imeCompositionActive = false;
                    ScheduleEvent(() => TriggerTextEditing(string.Empty, 0, 0));
                    break;
            }
        }

        #endregion

        protected override void HandleTouchFingerEvent(SDL_TouchFingerEvent evtTfinger)
        {
            if (evtTfinger.TryGetTouchName(out string name) && name == "pen")
            {
                // Windows Ink tablet/pen handling
                // InputManager expects to receive this as mouse events, to have proper `mouseSource` input priority (see InputManager.GetPendingInputs)
                // osu! expects to get tablet events as mouse events, and touch events as touch events for touch device (TD mod) handling (see https://github.com/ppy/osu/issues/25590)

                TriggerMouseMove(evtTfinger.x * ClientSize.Width, evtTfinger.y * ClientSize.Height);

                switch (evtTfinger.type)
                {
                    case SDL_EventType.SDL_FINGERDOWN:
                        TriggerMouseDown(MouseButton.Left);
                        break;

                    case SDL_EventType.SDL_FINGERUP:
                        TriggerMouseUp(MouseButton.Left);
                        break;
                }

                return;
            }

            base.HandleTouchFingerEvent(evtTfinger);
        }

        public override Size Size
        {
            protected set
            {
                // trick the game into thinking the borderless window has normal size so that it doesn't render into the extra space.
                if (applyBorderlessWindowHack && WindowState == WindowState.FullscreenBorderless)
                    value.Width -= windows_borderless_width_hack;

                base.Size = value;
            }
        }

        /// <summary>
        /// Amount of extra width added to window size when in borderless mode on Windows.
        /// Some drivers require this to avoid the window switching to exclusive fullscreen automatically.
        /// </summary>
        /// <remarks>Used on <see cref="GraphicsSurfaceType.OpenGL"/> and <see cref="GraphicsSurfaceType.Vulkan"/>.</remarks>
        private const int windows_borderless_width_hack = 1;

        protected override Size SetBorderless(Display display)
        {
            SDL_SetWindowBordered(SDLWindowHandle, SDL_bool.SDL_FALSE);

            var newSize = display.Bounds.Size;

            if (applyBorderlessWindowHack)
                // use the 1px hack we've always used, but only expand the width.
                // we also trick the game into thinking the window has normal size: see Size setter override
                newSize += new Size(windows_borderless_width_hack, 0);

            SDL_SetWindowSize(SDLWindowHandle, newSize.Width, newSize.Height);
            Position = display.Bounds.Location;

            return newSize;
        }

        /// <summary>
        /// On Windows, SDL will use the same image for both large and small icons (scaled as necessary).
        /// This can look bad if scaling down a large image, so we use the Windows API directly so as
        /// to get a cleaner icon set than SDL can provide.
        /// If called before the window has been created, or we do not find two separate icon sizes, we fall back to the base method.
        /// </summary>
        internal override void SetIconFromGroup(IconGroup iconGroup)
        {
            smallIcon = iconGroup.CreateIcon(small_icon_size, small_icon_size);
            largeIcon = iconGroup.CreateIcon(large_icon_size, large_icon_size);

            IntPtr windowHandle = WindowHandle;

            if (windowHandle == IntPtr.Zero || largeIcon == null || smallIcon == null)
                base.SetIconFromGroup(iconGroup);
            else
            {
                SendMessage(windowHandle, seticon_message, icon_small, smallIcon.Handle);
                SendMessage(windowHandle, seticon_message, icon_big, largeIcon.Handle);
            }
        }

        public override Point PointToClient(Point point)
        {
            ScreenToClient(WindowHandle, ref point);
            return point;
        }

        public override Point PointToScreen(Point point)
        {
            ClientToScreen(WindowHandle, ref point);
            return point;
        }

        [DllImport("SHCore.dll", SetLastError = true)]
        internal static extern bool SetProcessDpiAwareness(ProcessDpiAwareness awareness);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal enum ProcessDpiAwareness
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2,
        }

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT value);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal enum DPI_AWARENESS_CONTEXT
        {
            DPI_AWARENESS_CONTEXT_UNAWARE = -1,
            DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4,
            DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = -5,
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool ClientToScreen(IntPtr hWnd, ref Point point);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam
        );
    }
}
