// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using BenchmarkDotNet.Attributes;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.Framework.Benchmarks
{
    public partial class BenchmarkInvalidationWithManyNotAlive : GameBenchmark
    {
        [Test]
        [Benchmark]
        public void RunFrame() => RunSingleFrame();

        protected override Game CreateGame() => new TestGame();

        private partial class TestGame : Game
        {
            protected override void LoadComplete()
            {
                base.LoadComplete();

                var container = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                };

                for (int i = 0; i < 50000; i++)
                {
                    container.Add(
                        new Box
                        {
                            Size = new Vector2(10),
                            LifetimeStart = i > 10000 ? double.MaxValue : double.MinValue,
                        }
                    );
                }

                Add(container);

                container.Spin(200, RotationDirection.Clockwise);
            }
        }
    }
}
