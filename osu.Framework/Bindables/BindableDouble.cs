﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Framework.Bindables
{
    public class BindableDouble : BindableNumber<double>
    {
        public BindableDouble(double defaultValue = 0)
            : base(defaultValue) { }

        public override string ToString(string? format, IFormatProvider? formatProvider) =>
            base.ToString(format ?? "0.0###", formatProvider);

        protected override Bindable<double> CreateInstance() => new BindableDouble();
    }
}
