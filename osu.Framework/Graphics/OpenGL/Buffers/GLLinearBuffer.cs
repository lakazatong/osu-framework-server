﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osuTK.Graphics.ES30;

namespace osu.Framework.Graphics.OpenGL.Buffers
{
    internal static class GLLinearIndexData
    {
        static GLLinearIndexData()
        {
            GL.GenBuffers(1, out EBO_ID);
        }

        public static readonly int EBO_ID;
        public static int MaxAmountIndices;
    }

    /// <summary>
    /// This type of vertex buffer lets the ith vertex be referenced by the ith index.
    /// </summary>
    internal class GLLinearBuffer<T> : GLVertexBuffer<T>
        where T : unmanaged, IEquatable<T>, IVertex
    {
        private readonly int amountVertices;

        public GLLinearBuffer(
            GLRenderer renderer,
            int amountVertices,
            PrimitiveTopology topology,
            BufferUsageHint usage
        )
            : base(renderer, amountVertices, usage)
        {
            this.amountVertices = amountVertices;
            Topology = topology;

            Debug.Assert(amountVertices <= IRenderer.MAX_VERTICES);
        }

        protected override void Initialise()
        {
            base.Initialise();

            // Must be outside the conditional below as it needs to be added to the VAO
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, GLLinearIndexData.EBO_ID);

            if (amountVertices > GLLinearIndexData.MaxAmountIndices)
            {
                ushort[] indices = new ushort[amountVertices];

                for (int i = 0; i < amountVertices; i++)
                    indices[i] = (ushort)i;

                GL.BufferData(
                    BufferTarget.ElementArrayBuffer,
                    (IntPtr)(amountVertices * sizeof(ushort)),
                    indices,
                    BufferUsageHint.StaticDraw
                );

                GLLinearIndexData.MaxAmountIndices = amountVertices;
            }
        }

        protected override PrimitiveTopology Topology { get; }
    }
}
