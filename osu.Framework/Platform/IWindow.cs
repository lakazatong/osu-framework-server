// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace osu.Framework.Platform
{
    /// <summary>
    /// Interface representation of the game window, intended to hide any implementation-specific code.
    /// </summary>
    public interface IWindow : IDisposable
    {
        /// <summary>
        /// The graphics API for this window.
        /// </summary>
        internal IGraphicsSurface GraphicsSurface { get; }

        /// <summary>
        /// Cycles through the available <see cref="WindowMode"/>s as determined by <see cref="SupportedWindowModes"/>.
        /// </summary>
        void CycleMode();

        /// <summary>
        /// Configures the <see cref="IWindow"/> based on the provided <see cref="FrameworkConfigManager"/>.
        /// </summary>
        /// <param name="config">The configuration manager to use.</param>
        void SetupWindow(FrameworkConfigManager config);

        /// <summary>
        /// Creates the concrete window implementation.
        /// </summary>
        void Create();

        /// <summary>
        /// Start the window's run loop.
        /// Is a blocking call on desktop platforms, and a non-blocking call on mobile platforms.
        /// </summary>
        void Run();

        /// <summary>
        /// Invoked once a draw session has finished.
        /// </summary>
        void OnDraw();

        /// <summary>
        /// Forcefully closes the window.
        /// </summary>
        void Close();

        /// <summary>
        /// Invoked once every window event loop.
        /// </summary>
        event Action? Update;

        /// <summary>
        /// Invoked when the window close (X) button or another platform-native exit action has been pressed.
        /// </summary>
        event Action? ExitRequested;

        /// <summary>
        /// Invoked when the <see cref="IWindow"/> has closed.
        /// </summary>
        event Action? Exited;

        /// <summary>
        /// Invoked when the application associated with this <see cref="IWindow"/> has been suspended.
        /// </summary>
        event Action? Suspended;

        /// <summary>
        /// Invoked when the application associated with this <see cref="IWindow"/> has been resumed from suspension.
        /// </summary>
        event Action? Resumed;

        /// <summary>
        /// Invoked when the operating system is low on memory, in order for the application to free some.
        /// </summary>
        event Action? LowOnMemory;

        /// <summary>
        /// Invoked when the <see cref="IWindow"/> client size has changed.
        /// </summary>
        event Action? Resized;

        /// <summary>
        /// Invoked when the system keyboard layout has changed.
        /// </summary>
        event Action? KeymapChanged;

        /// <summary>
        /// Invoked when the user drops a file into the window.
        /// </summary>
        event Action<string>? DragDrop;

        /// <summary>
        /// Whether the OS cursor is currently contained within the game window.
        /// </summary>
        IBindable<bool> CursorInWindow { get; }

        /// <summary>
        /// Controls the state of the OS cursor.
        /// </summary>
        /// <remarks>If the cursor is <see cref="Platform.CursorState.Confined"/>, <see cref="CursorConfineRect"/> will be used.</remarks>
        CursorState CursorState { get; set; }

        /// <summary>
        /// Area to which the mouse cursor is confined to when <see cref="CursorState"/> is <see cref="Platform.CursorState.Confined"/>.
        /// </summary>
        /// <remarks>
        /// Will confine to the whole window by default (or when set to <c>null</c>).
        /// Supported fully on desktop platforms, and on Android when relative mode is enabled.
        /// </remarks>
        RectangleF? CursorConfineRect { get; set; }

        /// <summary>
        /// Controls the state of the window.
        /// </summary>
        WindowState WindowState { get; set; }

        /// <summary>
        /// Invoked when <see cref="WindowState"/> changes.
        /// </summary>
        event Action<WindowState>? WindowStateChanged;

        /// <summary>
        /// Returns the default <see cref="WindowMode"/> for the implementation.
        /// </summary>
        WindowMode DefaultWindowMode { get; }

        /// <summary>
        /// Whether this <see cref="IWindow"/> is active (in the foreground).
        /// </summary>
        IBindable<bool> IsActive { get; }

        /// <summary>
        /// Provides a <see cref="BindableSafeArea"/> that can be used to keep track of the "safe area" insets on mobile
        /// devices. This usually corresponds to areas of the screen hidden under notches and rounded corners.
        /// The safe area insets are provided by the operating system and dynamically change as the user rotates the device.
        /// </summary>
        BindableSafeArea SafeAreaPadding { get; }

        /// <summary>
        /// The <see cref="WindowMode"/>s supported by this <see cref="IWindow"/> implementation.
        /// </summary>
        IEnumerable<WindowMode> SupportedWindowModes { get; }

        /// <summary>
        /// Provides a <see cref="Bindable{WindowMode}"/> that manages the current window mode.
        /// Supported window modes for the current platform can be retrieved via <see cref="SupportedWindowModes"/>.
        /// </summary>
        Bindable<WindowMode> WindowMode { get; }

        /// <summary>
        /// Contains information about the current physical displays.
        /// </summary>
        ImmutableArray<Display> Displays { get; }

        /// <summary>
        /// Invoked when <see cref="Displays"/> has changed.
        /// </summary>
        event Action<IEnumerable<Display>>? DisplaysChanged;

        /// <summary>
        /// Gets the <see cref="Display"/> that has been set as "primary" or "default" in the operating system.
        /// </summary>
        Display PrimaryDisplay { get; }

        /// <summary>
        /// Exposes the <see cref="Display"/> that this window is currently on as a <see cref="Bindable{Display}"/>.
        /// </summary>
        Bindable<Display> CurrentDisplayBindable { get; }

        /// <summary>
        /// The <see cref="DisplayMode"/> for the display that this window is currently on.
        /// </summary>
        IBindable<DisplayMode> CurrentDisplayMode { get; }

        /// <summary>
        /// Attempts to raise the window, bringing it above other windows and requesting input focus.
        /// </summary>
        void Raise();

        /// <summary>
        /// Attempts to hide the window, making it invisible and hidden from the taskbar.
        /// </summary>
        void Hide();

        /// <summary>
        /// Attempts to show the window, making it visible.
        /// </summary>
        void Show();

        /// <summary>
        /// Attempts to flash the window in order to request the user's attention.
        /// </summary>
        /// <remarks>
        /// On platforms which don't support any kind of flashing (ie. mobile), this will be a no-op.
        /// </remarks>
        /// <param name="flashUntilFocused">
        /// When <c>true</c>, the window will flash until it is focused again.
        /// When <c>false</c> it will only flash momentarily.
        /// </param>
        void Flash(bool flashUntilFocused = false);

        /// <summary>
        /// Attempts to cancel any window flash requested with <see cref="Flash"/>.
        /// </summary>
        /// <remarks>
        /// On platforms which don't support any kind of flashing (ie. mobile), this will be a no-op.
        /// </remarks>
        void CancelFlash();

        /// <summary>
        /// Enable any system level timers that might dim or turn off the screen.
        /// </summary>
        void EnableScreenSuspension();

        /// <summary>
        /// Disable any system level timers that might dim or turn off the screen.
        /// </summary>
        void DisableScreenSuspension();

        /// <summary>
        /// Whether the window currently has focus.
        /// </summary>
        [Obsolete("Use IWindow.IsActive.Value instead.")] // can be removed 20250528
        bool Focused { get; }

        /// <summary>
        /// Sets the window icon to the provided <paramref name="imageStream"/>.
        /// </summary>
        void SetIconFromStream(Stream imageStream);

        /// <summary>
        /// Convert a screen based coordinate to local window space.
        /// </summary>
        /// <param name="point"></param>
        [Obsolete(
            "This member should not be used. It was never properly implemented for cross-platform use."
        )] // can be removed 20250528
        Point PointToClient(Point point);

        /// <summary>
        /// Convert a window based coordinate to global screen space.
        /// </summary>
        /// <param name="point"></param>
        [Obsolete(
            "This member should not be used. It was never properly implemented for cross-platform use."
        )] // can be removed 20250528
        Point PointToScreen(Point point);

        /// <summary>
        /// The client size of the window in pixels (excluding any window decoration/border).
        /// </summary>
        Size ClientSize { get; }

        /// <summary>
        /// The position of the window.
        /// </summary>
        Point Position { get; }

        /// <summary>
        /// The size of the window in scaled pixels (excluding any window decoration/border).
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// The ratio of <see cref="ClientSize"/> and <see cref="Size"/>.
        /// </summary>
        float Scale { get; }

        /// <summary>
        /// The minimum size of the window.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when setting a negative size, or a size greater than <see cref="MaxSize"/>.</exception>
        Size MinSize { get; set; }

        /// <summary>
        /// The maximum size of the window.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when setting a negative or zero size, or a size less than <see cref="MinSize"/>.</exception>
        Size MaxSize { get; set; }

        /// <summary>
        /// Gets or sets whether the window is user-resizable.
        /// </summary>
        bool Resizable { get; set; }

        /// <summary>
        /// The window title.
        /// </summary>
        string Title { get; set; }
    }
}
