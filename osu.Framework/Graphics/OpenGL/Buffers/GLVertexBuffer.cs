﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using osu.Framework.Development;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Statistics;
using osuTK.Graphics.ES30;
using SixLabors.ImageSharp.Memory;

namespace osu.Framework.Graphics.OpenGL.Buffers
{
    internal abstract class GLVertexBuffer<T> : IVertexBuffer, IDisposable
        where T : unmanaged, IEquatable<T>, IVertex
    {
        protected static readonly int STRIDE = GLVertexUtils<T>.STRIDE;

        protected readonly GLRenderer Renderer;
        private readonly BufferUsageHint usage;

        private Memory<T> vertexMemory;
        private IMemoryOwner<T>? memoryOwner;

        private bool isInitialised;
        private int vaoId;
        private int vboId;

        protected GLVertexBuffer(GLRenderer renderer, int amountVertices, BufferUsageHint usage)
        {
            Renderer = renderer;
            this.usage = usage;

            Size = amountVertices;
        }

        /// <summary>
        /// Sets the vertex at a specific index of this <see cref="GLVertexBuffer{T}"/>.
        /// </summary>
        /// <param name="vertexIndex">The index of the vertex.</param>
        /// <param name="vertex">The vertex.</param>
        /// <returns>Whether the vertex changed.</returns>
        public bool SetVertex(int vertexIndex, T vertex)
        {
            ref var currentVertex = ref getMemory().Span[vertexIndex];
            bool isNewVertex = !currentVertex.Equals(vertex);

            currentVertex = vertex;

            return isNewVertex;
        }

        /// <summary>
        /// Gets the number of vertices in this <see cref="GLVertexBuffer{T}"/>.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Initialises this <see cref="GLVertexBuffer{T}"/>. Guaranteed to be run on the draw thread.
        /// </summary>
        protected virtual void Initialise()
        {
            ThreadSafety.EnsureDrawThread();

            int size = Size * STRIDE;

            vboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)size, ref getMemory().Span[0], usage);

            GLVertexUtils<T>.SetAttributes();
        }

        ~GLVertexBuffer()
        {
            Renderer.ScheduleDisposal(v => v.Dispose(false), this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected bool IsDisposed { get; private set; }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            ((IVertexBuffer)this).Free();

            IsDisposed = true;
        }

        public void Bind(bool forRendering)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (!isInitialised)
            {
                Renderer.BindVertexArray(vaoId = GL.GenVertexArray());
                Initialise();
                isInitialised = true;
            }
            else
                Renderer.BindVertexArray(vaoId);
        }

        public virtual void Unbind() { }

        protected virtual int ToElements(int vertices) => vertices;

        protected virtual int ToElementIndex(int vertexIndex) => vertexIndex;

        protected abstract PrimitiveTopology Topology { get; }

        public void Draw()
        {
            DrawRange(0, Size);
        }

        public void DrawRange(int startIndex, int endIndex)
        {
            Bind(true);
            Renderer.DrawVertices(
                Topology,
                ToElementIndex(startIndex),
                ToElements(endIndex - startIndex)
            );
        }

        public void Update()
        {
            UpdateRange(0, Size);
        }

        public void UpdateRange(int startIndex, int endIndex)
        {
            Bind(false);

            int countVertices = endIndex - startIndex;

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);
            GL.BufferSubData(
                BufferTarget.ArrayBuffer,
                startIndex * STRIDE,
                (IntPtr)(countVertices * STRIDE),
                ref getMemory().Span[startIndex]
            );

            FrameStatistics.Add(StatisticsCounterType.VerticesUpl, countVertices);
        }

        private ref Memory<T> getMemory()
        {
            ThreadSafety.EnsureDrawThread();

            if (!InUse)
            {
                memoryOwner =
                    SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate<T>(
                        Size,
                        AllocationOptions.Clean
                    );
                vertexMemory = memoryOwner.Memory;

                Renderer.RegisterVertexBufferUse(this);
            }

            LastUseFrameIndex = Renderer.FrameIndex;

            return ref vertexMemory;
        }

        public ulong LastUseFrameIndex { get; private set; }

        public bool InUse => LastUseFrameIndex > 0;

        void IVertexBuffer.Free()
        {
            if (isInitialised)
            {
                GL.DeleteBuffer(vboId);
                GL.DeleteVertexArray(vaoId);
            }

            memoryOwner?.Dispose();
            memoryOwner = null;
            vertexMemory = Memory<T>.Empty;

            LastUseFrameIndex = 0;

            isInitialised = false;
        }
    }
}
