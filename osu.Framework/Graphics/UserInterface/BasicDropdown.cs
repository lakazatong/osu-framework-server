﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace osu.Framework.Graphics.UserInterface
{
    public partial class BasicDropdown<T> : Dropdown<T>
    {
        protected override DropdownMenu CreateMenu() => new BasicDropdownMenu();

        protected override DropdownHeader CreateHeader() => new BasicDropdownHeader();

        public partial class BasicDropdownHeader : DropdownHeader
        {
            private static FontUsage font => FrameworkFont.Condensed;

            private readonly SpriteText label;

            protected internal override LocalisableString Label
            {
                get => label.Text;
                set => label.Text = value;
            }

            public BasicDropdownHeader()
            {
                Foreground.Padding = new MarginPadding(5);
                BackgroundColour = FrameworkColour.Green;
                BackgroundColourHover = FrameworkColour.YellowGreen;

                Children = new[]
                {
                    label = new SpriteText
                    {
                        AlwaysPresent = true,
                        Font = font,
                        Height = font.Size,
                    },
                };
            }

            protected override DropdownSearchBar CreateSearchBar() => new BasicDropdownSearchBar();

            public partial class BasicDropdownSearchBar : DropdownSearchBar
            {
                protected override void PopIn() => this.FadeIn();

                protected override void PopOut() => this.FadeOut();

                protected override TextBox CreateTextBox() =>
                    new BasicTextBox { PlaceholderText = "type to search", FontSize = font.Size };
            }
        }

        public partial class BasicDropdownMenu : DropdownMenu
        {
            protected override Menu CreateSubMenu() => new BasicMenu(Direction.Vertical);

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(
                MenuItem item
            ) => new DrawableBasicDropdownMenuItem(item);

            protected override ScrollContainer<Drawable> CreateScrollContainer(
                Direction direction
            ) => new BasicScrollContainer(direction);

            private partial class DrawableBasicDropdownMenuItem : DrawableDropdownMenuItem
            {
                public DrawableBasicDropdownMenuItem(MenuItem item)
                    : base(item)
                {
                    Foreground.Padding = new MarginPadding(2);
                    BackgroundColour = FrameworkColour.BlueGreen;
                    BackgroundColourHover = FrameworkColour.Green;
                    BackgroundColourSelected = FrameworkColour.GreenDark;
                }

                protected override Drawable CreateContent() =>
                    new SpriteText { Font = FrameworkFont.Condensed };
            }
        }
    }
}
