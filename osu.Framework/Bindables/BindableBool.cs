﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Framework.Bindables
{
    public class BindableBool : Bindable<bool>
    {
        public BindableBool(bool value = false)
            : base(value) { }

        public override void Parse(object? input, IFormatProvider provider)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input is "1")
                Value = true;
            else if (input is "0")
                Value = false;
            else
                base.Parse(input, provider);
        }

        public void Toggle() => Value = !Value;

        protected override Bindable<bool> CreateInstance() => new BindableBool();
    }
}
