// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Extensions.ImageExtensions;
using osu.Framework.Logging;
using osu.Framework.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static SDL2.SDL;
using Image = SixLabors.ImageSharp.Image;
using Point = System.Drawing.Point;

namespace osu.Framework.Platform.SDL2
{
    /// <summary>
    /// Default implementation of a window, using SDL for windowing and graphics support.
    /// </summary>
    internal abstract partial class SDL2Window : ISDLWindow
    {
        internal IntPtr SDLWindowHandle { get; private set; } = IntPtr.Zero;

        private readonly SDL2GraphicsSurface graphicsSurface;
        IGraphicsSurface IWindow.GraphicsSurface => graphicsSurface;

        /// <summary>
        /// Returns true if window has been created.
        /// Returns false if the window has not yet been created, or has been closed.
        /// </summary>
        public bool Exists { get; private set; }

        public BindableSafeArea SafeAreaPadding { get; } = new BindableSafeArea();

        public virtual Point PointToClient(Point point) => point;

        public virtual Point PointToScreen(Point point) => point;

        private const int default_width = 1366;
        private const int default_height = 768;

        private const int default_icon_size = 256;

        /// <summary>
        /// Scheduler for actions to run before the next event loop.
        /// </summary>
        private readonly Scheduler commandScheduler = new Scheduler();

        /// <summary>
        /// Scheduler for actions to run at the end of the current event loop.
        /// </summary>
        protected readonly Scheduler EventScheduler = new Scheduler();

        private string title = string.Empty;

        /// <summary>
        /// Gets and sets the window title.
        /// </summary>
        public string Title
        {
            get => title;
            set
            {
                title = value;
                ScheduleCommand(() => SDL_SetWindowTitle(SDLWindowHandle, title));
            }
        }

        /// <summary>
        /// Whether the current display server is Wayland.
        /// </summary>
        internal bool IsWayland
        {
            get
            {
                if (SDLWindowHandle == IntPtr.Zero)
                    return false;

                return GetWindowSystemInformation().subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_WAYLAND;
            }
        }

        /// <summary>
        /// Gets the native window handle as provided by the operating system.
        /// </summary>
        public IntPtr WindowHandle
        {
            get
            {
                if (SDLWindowHandle == IntPtr.Zero)
                    return IntPtr.Zero;

                var wmInfo = GetWindowSystemInformation();

                // Window handle is selected per subsystem as defined at:
                // https://wiki.libsdl.org/SDL_SysWMinfo
                switch (wmInfo.subsystem)
                {
                    case SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS:
                        return wmInfo.info.win.window;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_X11:
                        return wmInfo.info.x11.window;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_DIRECTFB:
                        return wmInfo.info.dfb.window;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_COCOA:
                        return wmInfo.info.cocoa.window;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_UIKIT:
                        return wmInfo.info.uikit.window;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_WAYLAND:
                        return wmInfo.info.wl.surface;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_ANDROID:
                        return wmInfo.info.android.window;

                    default:
                        return IntPtr.Zero;
                }
            }
        }

        public IntPtr DisplayHandle
        {
            get
            {
                if (SDLWindowHandle == IntPtr.Zero)
                    return IntPtr.Zero;

                var wmInfo = GetWindowSystemInformation();

                switch (wmInfo.subsystem)
                {
                    case SDL_SYSWM_TYPE.SDL_SYSWM_X11:
                        return wmInfo.info.x11.display;

                    case SDL_SYSWM_TYPE.SDL_SYSWM_WAYLAND:
                        return wmInfo.info.wl.display;

                    default:
                        return IntPtr.Zero;
                }
            }
        }

        internal SDL_SysWMinfo GetWindowSystemInformation()
        {
            if (SDLWindowHandle == IntPtr.Zero)
                return default;

            var wmInfo = new SDL_SysWMinfo();
            SDL_GetVersion(out wmInfo.version);
            SDL_GetWindowWMInfo(SDLWindowHandle, ref wmInfo);
            return wmInfo;
        }

        public bool CapsLockPressed => SDL_GetModState().HasFlagFast(SDL_Keymod.KMOD_CAPS);

        public bool KeyboardAttached => true; // SDL2 has no way of knowing whether a keyboard is attached, assume true.

        // references must be kept to avoid GC, see https://stackoverflow.com/a/6193914

        [UsedImplicitly]
        private SDL_LogOutputFunction logOutputDelegate;

        [UsedImplicitly]
        private SDL_EventFilter? eventFilterDelegate;

        [UsedImplicitly]
        private SDL_EventFilter? eventWatchDelegate;

        /// <summary>
        /// Represents a handle to this <see cref="SDL2Window"/> instance, used for unmanaged callbacks.
        /// </summary>
        protected ObjectHandle<SDL2Window> ObjectHandle { get; private set; }

        protected SDL2Window(GraphicsSurfaceType surfaceType, string appName)
        {
            ObjectHandle = new ObjectHandle<SDL2Window>(this, GCHandleType.Normal);

            SDL_SetHint(SDL_HINT_APP_NAME, appName);

            if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_GAMECONTROLLER) < 0)
            {
                throw new InvalidOperationException($"Failed to initialise SDL: {SDL_GetError()}");
            }

            SDL_GetVersion(out SDL_version version);
            Logger.Log(
                $@"SDL2 Initialized
                          SDL2 Version: {version.major}.{version.minor}.{version.patch}
                          SDL2 Revision: {SDL_GetRevision()}
                          SDL2 Video driver: {SDL_GetCurrentVideoDriver()}"
            );

            SDL_LogSetPriority(
                (int)SDL_LogCategory.SDL_LOG_CATEGORY_ERROR,
                SDL_LogPriority.SDL_LOG_PRIORITY_DEBUG
            );
            SDL_LogSetOutputFunction(logOutputDelegate = logOutput, IntPtr.Zero);

            graphicsSurface = new SDL2GraphicsSurface(this, surfaceType);

            CursorStateBindable.ValueChanged += evt =>
            {
                updateCursorVisibility(!evt.NewValue.HasFlagFast(CursorState.Hidden));
                updateCursorConfinement();
            };

            populateJoysticks();
        }

        [MonoPInvokeCallback(typeof(SDL_LogOutputFunction))]
        private static void logOutput(
            IntPtr _,
            int categoryInt,
            SDL_LogPriority priority,
            IntPtr messagePtr
        )
        {
            var category = (SDL_LogCategory)categoryInt;
            string? message = Marshal.PtrToStringUTF8(messagePtr);

            Logger.Log(
                $@"SDL {category.ReadableName()} log [{priority.ReadableName()}]: {message}"
            );
        }

        public void SetupWindow(FrameworkConfigManager config)
        {
            setupWindowing(config);
            setupInput(config);
        }

        public virtual void Create()
        {
            SDL_WindowFlags flags =
                SDL_WindowFlags.SDL_WINDOW_RESIZABLE
                | SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI
                | SDL_WindowFlags.SDL_WINDOW_HIDDEN; // shown after first swap to avoid white flash on startup (windows)

            flags |= WindowState.ToFlags();
            flags |= graphicsSurface.Type.ToFlags();

            SDL_SetHint(SDL_HINT_WINDOWS_NO_CLOSE_ON_ALT_F4, "1");
            SDL_SetHint(SDL_HINT_IME_SHOW_UI, "1");
            SDL_SetHint(SDL_HINT_MOUSE_RELATIVE_MODE_CENTER, "0");
            SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "0"); // disable touch events generating synthetic mouse events on desktop platforms
            SDL_SetHint(SDL_HINT_MOUSE_TOUCH_EVENTS, "0"); // disable mouse events generating synthetic touch events on mobile platforms

            // we want text input to only be active when SDL2DesktopWindowTextInput is active.
            // SDL activates it by default on some platforms: https://github.com/libsdl-org/SDL/blob/release-2.0.16/src/video/SDL_video.c#L573-L582
            // so we deactivate it on startup.
            SDL_StopTextInput();

            SDLWindowHandle = SDL_CreateWindow(
                title,
                Position.X,
                Position.Y,
                Size.Width,
                Size.Height,
                flags
            );

            if (SDLWindowHandle == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to create SDL window. SDL Error: {SDL_GetError()}"
                );

            graphicsSurface.Initialise();

            initialiseWindowingAfterCreation();
            Exists = true;
        }

        /// <summary>
        /// Starts the window's run loop.
        /// </summary>
        public void Run()
        {
            SDL_SetEventFilter(eventFilterDelegate = eventFilter, ObjectHandle.Handle);
            SDL_AddEventWatch(eventWatchDelegate = eventWatch, ObjectHandle.Handle);

            RunMainLoop();
        }

        /// <summary>
        /// Runs the main window loop.
        /// </summary>
        /// <remarks>
        /// By default this will block and indefinitely call <see cref="RunFrame"/> as long as the window <see cref="Exists"/>.
        /// Once the main loop finished running, cleanup logic will run.
        ///
        /// This may be overridden for special use cases, like mobile platforms which delegate execution of frames to the OS
        /// and don't require any kind of exit logic to exist.
        /// </remarks>
        protected virtual void RunMainLoop()
        {
            while (Exists)
                RunFrame();

            Exited?.Invoke();
            Close();
            SDL_Quit();
        }

        /// <summary>
        /// Run a single frame.
        /// </summary>
        protected void RunFrame()
        {
            commandScheduler.Update();

            if (!Exists)
                return;

            if (pendingWindowState != null)
                updateAndFetchWindowSpecifics();

            pollSDLEvents();

            if (!cursorInWindow.Value)
                pollMouse();

            EventScheduler.Update();
            Update?.Invoke();
        }

        /// <summary>
        /// Handles <see cref="SDL_Event"/>s fired from the SDL event filter.
        /// </summary>
        /// <remarks>
        /// As per SDL's recommendation, application events should always be handled via the event filter.
        /// See: https://wiki.libsdl.org/SDL2/SDL_EventType#android_ios_and_winrt_events
        /// </remarks>
        protected virtual void HandleEventFromFilter(SDL_Event evt)
        {
            switch (evt.type)
            {
                case SDL_EventType.SDL_APP_TERMINATING:
                    handleQuitEvent(evt.quit);
                    break;

                case SDL_EventType.SDL_APP_DIDENTERBACKGROUND:
                    Suspended?.Invoke();
                    break;

                case SDL_EventType.SDL_APP_WILLENTERFOREGROUND:
                    Resumed?.Invoke();
                    break;

                case SDL_EventType.SDL_APP_LOWMEMORY:
                    LowOnMemory?.Invoke();
                    break;
            }
        }

        protected void HandleEventFromWatch(SDL_Event evt)
        {
            switch (evt.type)
            {
                case SDL_EventType.SDL_WINDOWEVENT:
                    // polling via SDL_PollEvent blocks on resizes (https://stackoverflow.com/a/50858339)
                    if (
                        evt.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED
                        && !updatingWindowStateAndSize
                    )
                        fetchWindowSize();

                    break;
            }
        }

        [MonoPInvokeCallback(typeof(SDL_EventFilter))]
        private static int eventFilter(IntPtr userdata, IntPtr eventPtr)
        {
            var handle = new ObjectHandle<SDL2Window>(userdata);
            if (handle.GetTarget(out SDL2Window window))
                window.HandleEventFromFilter(Marshal.PtrToStructure<SDL_Event>(eventPtr));

            return 1;
        }

        [MonoPInvokeCallback(typeof(SDL_EventFilter))]
        private static int eventWatch(IntPtr userdata, IntPtr eventPtr)
        {
            var handle = new ObjectHandle<SDL2Window>(userdata);
            if (handle.GetTarget(out SDL2Window window))
                window.HandleEventFromWatch(Marshal.PtrToStructure<SDL_Event>(eventPtr));

            return 1;
        }

        private bool firstDraw = true;

        public void OnDraw()
        {
            if (!firstDraw)
                return;

            Visible = true;
            firstDraw = false;
        }

        /// <summary>
        /// Forcefully closes the window.
        /// </summary>
        public void Close()
        {
            if (Exists)
            {
                // Close will be called as part of finishing the Run loop.
                ScheduleCommand(() => Exists = false);
            }
            else
            {
                if (SDLWindowHandle != IntPtr.Zero)
                {
                    SDL_DestroyWindow(SDLWindowHandle);
                    SDLWindowHandle = IntPtr.Zero;
                }
            }
        }

        public void Raise() =>
            ScheduleCommand(() =>
            {
                var flags = (SDL_WindowFlags)SDL_GetWindowFlags(SDLWindowHandle);

                if (flags.HasFlagFast(SDL_WindowFlags.SDL_WINDOW_MINIMIZED))
                    SDL_RestoreWindow(SDLWindowHandle);

                SDL_RaiseWindow(SDLWindowHandle);
            });

        public void Hide() =>
            ScheduleCommand(() =>
            {
                SDL_HideWindow(SDLWindowHandle);
            });

        public void Show() =>
            ScheduleCommand(() =>
            {
                SDL_ShowWindow(SDLWindowHandle);
            });

        public void Flash(bool flashUntilFocused = false) =>
            ScheduleCommand(() =>
            {
                if (isActive.Value)
                    return;

                if (!RuntimeInfo.IsDesktop)
                    return;

                SDL_FlashWindow(
                    SDLWindowHandle,
                    flashUntilFocused
                        ? SDL_FlashOperation.SDL_FLASH_UNTIL_FOCUSED
                        : SDL_FlashOperation.SDL_FLASH_BRIEFLY
                );
            });

        public void CancelFlash() =>
            ScheduleCommand(() =>
            {
                if (!RuntimeInfo.IsDesktop)
                    return;

                SDL_FlashWindow(SDLWindowHandle, SDL_FlashOperation.SDL_FLASH_CANCEL);
            });

        public void EnableScreenSuspension() => ScheduleCommand(SDL_EnableScreenSaver);

        public void DisableScreenSuspension() => ScheduleCommand(SDL_DisableScreenSaver);

        /// <summary>
        /// Attempts to set the window's icon to the specified image.
        /// </summary>
        /// <param name="image">An <see cref="Image{Rgba32}"/> to set as the window icon.</param>
        private unsafe void setSDLIcon(Image<Rgba32> image)
        {
            var pixelMemory = image.CreateReadOnlyPixelMemory();
            var imageSize = image.Size;

            ScheduleCommand(() =>
            {
                var pixelSpan = pixelMemory.Span;

                IntPtr surface;
                fixed (Rgba32* ptr = pixelSpan)
                    surface = SDL_CreateRGBSurfaceFrom(
                        new IntPtr(ptr),
                        imageSize.Width,
                        imageSize.Height,
                        32,
                        imageSize.Width * 4,
                        0xff,
                        0xff00,
                        0xff0000,
                        0xff000000
                    );

                SDL_SetWindowIcon(SDLWindowHandle, surface);
                SDL_FreeSurface(surface);
            });
        }

        #region SDL Event Handling

        /// <summary>
        /// Adds an <see cref="Action"/> to the <see cref="Scheduler"/> expected to handle event callbacks.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to execute.</param>
        protected void ScheduleEvent(Action action) => EventScheduler.Add(action, false);

        protected void ScheduleCommand(Action action) => commandScheduler.Add(action, false);

        private const int events_per_peep = 64;
        private readonly SDL_Event[] events = new SDL_Event[events_per_peep];

        /// <summary>
        /// Poll for all pending events.
        /// </summary>
        private void pollSDLEvents()
        {
            SDL_PumpEvents();

            int eventsRead;

            do
            {
                eventsRead = SDL_PeepEvents(
                    events,
                    events_per_peep,
                    SDL_eventaction.SDL_GETEVENT,
                    SDL_EventType.SDL_FIRSTEVENT,
                    SDL_EventType.SDL_LASTEVENT
                );
                for (int i = 0; i < eventsRead; i++)
                    HandleEvent(events[i]);
            } while (eventsRead == events_per_peep);
        }

        /// <summary>
        /// Handles <see cref="SDL_Event"/>s polled on the main thread.
        /// </summary>
        protected virtual void HandleEvent(SDL_Event e)
        {
            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT:
                    handleQuitEvent(e.quit);
                    break;

                case SDL_EventType.SDL_DISPLAYEVENT:
                    handleDisplayEvent(e.display);
                    break;

                case SDL_EventType.SDL_WINDOWEVENT:
                    handleWindowEvent(e.window);
                    break;

                case SDL_EventType.SDL_KEYDOWN:
                case SDL_EventType.SDL_KEYUP:
                    handleKeyboardEvent(e.key);
                    break;

                case SDL_EventType.SDL_TEXTEDITING:
                    HandleTextEditingEvent(e.edit);
                    break;

                case SDL_EventType.SDL_TEXTINPUT:
                    HandleTextInputEvent(e.text);
                    break;

                case SDL_EventType.SDL_KEYMAPCHANGED:
                    handleKeymapChangedEvent();
                    break;

                case SDL_EventType.SDL_MOUSEMOTION:
                    handleMouseMotionEvent(e.motion);
                    break;

                case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                case SDL_EventType.SDL_MOUSEBUTTONUP:
                    handleMouseButtonEvent(e.button);
                    break;

                case SDL_EventType.SDL_MOUSEWHEEL:
                    handleMouseWheelEvent(e.wheel);
                    break;

                case SDL_EventType.SDL_JOYAXISMOTION:
                    handleJoyAxisEvent(e.jaxis);
                    break;

                case SDL_EventType.SDL_JOYBALLMOTION:
                    handleJoyBallEvent(e.jball);
                    break;

                case SDL_EventType.SDL_JOYHATMOTION:
                    handleJoyHatEvent(e.jhat);
                    break;

                case SDL_EventType.SDL_JOYBUTTONDOWN:
                case SDL_EventType.SDL_JOYBUTTONUP:
                    handleJoyButtonEvent(e.jbutton);
                    break;

                case SDL_EventType.SDL_JOYDEVICEADDED:
                case SDL_EventType.SDL_JOYDEVICEREMOVED:
                    handleJoyDeviceEvent(e.jdevice);
                    break;

                case SDL_EventType.SDL_CONTROLLERAXISMOTION:
                    handleControllerAxisEvent(e.caxis);
                    break;

                case SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                case SDL_EventType.SDL_CONTROLLERBUTTONUP:
                    handleControllerButtonEvent(e.cbutton);
                    break;

                case SDL_EventType.SDL_CONTROLLERDEVICEADDED:
                case SDL_EventType.SDL_CONTROLLERDEVICEREMOVED:
                case SDL_EventType.SDL_CONTROLLERDEVICEREMAPPED:
                    handleControllerDeviceEvent(e.cdevice);
                    break;

                case SDL_EventType.SDL_FINGERDOWN:
                case SDL_EventType.SDL_FINGERUP:
                case SDL_EventType.SDL_FINGERMOTION:
                    HandleTouchFingerEvent(e.tfinger);
                    break;

                case SDL_EventType.SDL_DROPFILE:
                case SDL_EventType.SDL_DROPTEXT:
                case SDL_EventType.SDL_DROPBEGIN:
                case SDL_EventType.SDL_DROPCOMPLETE:
                    handleDropEvent(e.drop);
                    break;
            }
        }

        // ReSharper disable once UnusedParameter.Local
        private void handleQuitEvent(SDL_QuitEvent evtQuit) => ExitRequested?.Invoke();

        #endregion

        public void SetIconFromStream(Stream imageStream)
        {
            using (var ms = new MemoryStream())
            {
                imageStream.CopyTo(ms);
                ms.Position = 0;

                try
                {
                    SetIconFromImage(Image.Load<Rgba32>(ms.GetBuffer()));
                }
                catch
                {
                    if (IconGroup.TryParse(ms.GetBuffer(), out var iconGroup))
                        SetIconFromGroup(iconGroup);
                }
            }
        }

        internal virtual void SetIconFromGroup(IconGroup iconGroup)
        {
            // LoadRawIcon returns raw PNG data if available, which avoids any Windows-specific pinvokes
            byte[]? bytes = iconGroup.LoadRawIcon(default_icon_size, default_icon_size);
            if (bytes == null)
                return;

            SetIconFromImage(Image.Load<Rgba32>(bytes));
        }

        internal virtual void SetIconFromImage(Image<Rgba32> iconImage) => setSDLIcon(iconImage);

        #region Events

        /// <summary>
        /// Invoked once every window event loop.
        /// </summary>
        public event Action? Update;

        /// <summary>
        /// Invoked when the application associated with this <see cref="IWindow"/> has been suspended.
        /// </summary>
        public event Action? Suspended;

        /// <summary>
        /// Invoked when the application associated with this <see cref="IWindow"/> has been resumed from suspension.
        /// </summary>
        public event Action? Resumed;

        /// <summary>
        /// Invoked when the operating system is low on memory, in order for the application to free some.
        /// </summary>
        public event Action? LowOnMemory;

        /// <summary>
        /// Invoked when the window close (X) button or another platform-native exit action has been pressed.
        /// </summary>
        public event Action? ExitRequested;

        /// <summary>
        /// Invoked when the window is about to close.
        /// </summary>
        public event Action? Exited;

        /// <summary>
        /// Invoked when the user drops a file into the window.
        /// </summary>
        public event Action<string>? DragDrop;

        #endregion

        public void Dispose()
        {
            Close();
            SDL_Quit();

            ObjectHandle.Dispose();
        }
    }
}
