// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osuTK;
using osuTK.Input;

namespace osu.Framework.Tests.Input
{
    [HeadlessTest]
    public partial class MouseInputTest : ManualInputManagerTestScene
    {
        /// <summary>
        /// Tests that a drawable that is removed from the hierarchy (or is otherwise removed from the input queues) does not receive an OnClick() event on mouse up.
        /// </summary>
        [Test]
        public void TestNoLongerValidDrawableDoesNotReceiveClick()
        {
            var receptors = new InputReceptor[2];

            AddStep(
                "create hierarchy",
                () =>
                {
                    Children = new Drawable[]
                    {
                        receptors[0] = new InputReceptor { Size = new Vector2(100) },
                        receptors[1] = new InputReceptor { Size = new Vector2(100) },
                    };
                }
            );

            AddStep("move mouse to receptors", () => InputManager.MoveMouseTo(receptors[0]));
            AddStep("press button", () => InputManager.PressButton(MouseButton.Left));

            AddStep("remove receptor 0", () => Remove(receptors[0], true));

            AddStep("release button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("receptor 0 did not receive click", () => !receptors[0].ClickReceived);
        }

        /// <summary>
        /// Tests that a drawable that is removed and re-added to the hierarchy still receives an OnClick() event.
        /// </summary>
        [Test]
        public void TestReValidatedDrawableReceivesClick()
        {
            var receptors = new InputReceptor[2];

            AddStep(
                "create hierarchy",
                () =>
                {
                    Children = new Drawable[]
                    {
                        receptors[0] = new InputReceptor { Size = new Vector2(100) },
                        receptors[1] = new InputReceptor { Size = new Vector2(100) },
                    };
                }
            );

            AddStep("move mouse to receptors", () => InputManager.MoveMouseTo(receptors[0]));
            AddStep("press button", () => InputManager.PressButton(MouseButton.Left));

            AddStep("remove receptor 0", () => Remove(receptors[0], false));
            AddStep("add back receptor 0", () => Add(receptors[0]));

            AddStep("release button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("receptor 0 received click", () => receptors[0].ClickReceived);
        }

        /// <summary>
        /// Tests that a drawable that is removed from the hierarchy (or is otherwise removed from the input queues) does not receive an OnDoubleClick() event.
        /// </summary>
        [Test]
        public void TestNoLongerValidDrawableDoesNotReceiveDoubleClick()
        {
            InputReceptor receptor = null;

            AddStep(
                "create hierarchy",
                () =>
                {
                    Child = receptor = new InputReceptor
                    {
                        Size = new Vector2(100),
                        Click = () => true,
                    };
                }
            );

            AddStep("move mouse to receptor", () => InputManager.MoveMouseTo(receptor));
            AddStep("click", () => InputManager.Click(MouseButton.Left));

            AddStep(
                "remove receptor and double click",
                () =>
                {
                    Remove(receptor, true); // Test correctness can be asserted by removing this line and ensuring the test fails
                    InputManager.Click(MouseButton.Left); // Done immediately after due to double clicks being frame-dependent (timing)
                }
            );

            AddAssert("receptor did not receive double click", () => !receptor.DoubleClickReceived);
        }

        /// <summary>
        /// Tests that a drawable whose parent is removed from the hierarchy (or is otherwise removed from the input queues) does not receive an OnDragStart() event.
        /// </summary>
        [Test]
        public void TestNoLongerValidChildDrawableDoesNotReceiveDragStart()
        {
            InputReceptor receptor = null;
            Container receptorParent = null;
            Vector2 lastPosition = Vector2.Zero;

            AddStep(
                "create hierarchy",
                () =>
                {
                    Children = new Drawable[]
                    {
                        receptorParent = new Container
                        {
                            Children = new Drawable[]
                            {
                                receptor = new InputReceptor { Size = new Vector2(100) },
                            },
                        },
                    };
                }
            );

            AddStep(
                "move mouse to receptor",
                () =>
                {
                    lastPosition = receptor.ToScreenSpace(receptor.LayoutRectangle.Centre);
                    InputManager.MoveMouseTo(lastPosition);
                }
            );
            AddStep("press button", () => InputManager.PressButton(MouseButton.Left));

            AddStep("remove receptor parent", () => Remove(receptorParent, true));

            AddStep("drag mouse", () => InputManager.MoveMouseTo(lastPosition + new Vector2(10)));
            AddAssert("receptor did not receive drag start", () => !receptor.DragStartReceived);
        }

        private partial class InputReceptor : Box
        {
            public bool ClickReceived { get; set; }
            public bool DoubleClickReceived { get; set; }
            public bool DragStartReceived { get; set; }

            public Func<bool> Click;

            protected override bool OnClick(ClickEvent e)
            {
                ClickReceived = true;
                return Click?.Invoke() ?? false;
            }

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                DoubleClickReceived = true;
                return true;
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                DragStartReceived = true;
                return true;
            }
        }
    }
}
