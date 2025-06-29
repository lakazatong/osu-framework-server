﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Framework.Platform.Apple.Native
{
    internal static partial class Class
    {
        [LibraryImport(Interop.LIB_OBJ_C, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr class_replaceMethod(
            IntPtr classHandle,
            IntPtr selector,
            IntPtr method,
            string types
        );

        [LibraryImport(Interop.LIB_OBJ_C)]
        private static partial IntPtr class_getInstanceMethod(IntPtr classHandle, IntPtr selector);

        [LibraryImport(Interop.LIB_OBJ_C)]
        private static partial void method_exchangeImplementations(IntPtr method1, IntPtr method2);

        [LibraryImport(Interop.LIB_OBJ_C, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_getClass(string name);

        public static IntPtr Get(string name)
        {
            IntPtr id = objc_getClass(name);
            if (id == IntPtr.Zero)
                throw new ArgumentException("Unknown class: " + name);

            return id;
        }

        public static void RegisterMethod(
            IntPtr handle,
            Delegate action,
            string selector,
            string typeString
        ) =>
            class_replaceMethod(
                handle,
                Selector.Get(selector),
                Marshal.GetFunctionPointerForDelegate(action),
                typeString
            );

        /// <summary>
        /// Performs method swizzling for a given selector, using a given delegate implementation.
        /// </summary>
        /// <remarks>
        /// This essentially adds a new Objective-C method, then swaps the implementation with an existing one.
        /// Returns a selector to the newly registered method, which has the original implementation of
        /// <paramref name="selector"/> before swizzling.
        /// https://nshipster.com/method-swizzling/
        /// </remarks>
        /// <param name="classHandle">The Objective-C class which should have a method swizzled.</param>
        /// <param name="selector">The selector to swizzle.</param>
        /// <param name="typeString">The type encoding of the selector.</param>
        /// <param name="action">The delegate to use as the new implementation.</param>
        /// <returns>A selector for the newly registered method, containing the old implementation.</returns>
        public static IntPtr SwizzleMethod(
            IntPtr classHandle,
            string selector,
            string typeString,
            Delegate action
        )
        {
            IntPtr targetSelector = Selector.Get(selector);
            IntPtr targetMethod = class_getInstanceMethod(classHandle, targetSelector);
            IntPtr newMethodImplementation = Marshal.GetFunctionPointerForDelegate(action);
            IntPtr newSelector = Selector.Get($"orig_{selector}");
            class_replaceMethod(classHandle, newSelector, newMethodImplementation, typeString);
            IntPtr newMethod = class_getInstanceMethod(classHandle, newSelector);
            method_exchangeImplementations(targetMethod, newMethod);
            return newSelector;
        }
    }
}
