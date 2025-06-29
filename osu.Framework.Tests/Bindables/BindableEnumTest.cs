﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace osu.Framework.Tests.Bindables
{
    [TestFixture]
    public class BindableEnumTest
    {
        [TestCase(TestEnum.Value1)]
        [TestCase(TestEnum.Value2)]
        [TestCase(TestEnum.Value1 - 1)]
        [TestCase(TestEnum.Value2 + 1)]
        public void TestSet(TestEnum value)
        {
            var bindable = new Bindable<TestEnum> { Value = value };
            Assert.AreEqual(value, bindable.Value);
        }

        [TestCase(TestEnum.Value1, "Value1", 0, 0f, 0d, 0L, (short)0, (sbyte)0)]
        [TestCase(TestEnum.Value2, "Value2", 1, 1f, 1d, 1L, (short)1, (sbyte)1)]
        [TestCase(TestEnum.Value1 - 1, "-1", -1, -1f, -1d, -1L, (short)-1, (sbyte)-1)]
        [TestCase(TestEnum.Value2 + 1, "2", 2, 2f, 2d, 2L, (short)2, (sbyte)2)]
        public void TestParsing(TestEnum expected, params object[] values)
        {
            var bindable = new Bindable<TestEnum>();
            var nullable = new Bindable<TestEnum?>();

            foreach (object value in values.Append(expected))
            {
                bindable.Parse(value, CultureInfo.InvariantCulture);
                nullable.Parse(value, CultureInfo.InvariantCulture);

                Assert.AreEqual(expected, bindable.Value);
                Assert.AreEqual(expected, nullable.Value);
            }
        }

        [TestCase(1.1f)]
        [TestCase("Not a value")]
        public void TestUnparsaebles(object value)
        {
            var bindable = new Bindable<TestEnum>();
            var nullable = new Bindable<TestEnum?>();

            Assert.Throws<ArgumentException>(() =>
                bindable.Parse(value, CultureInfo.InvariantCulture)
            );
            Assert.Throws<ArgumentException>(() =>
                nullable.Parse(value, CultureInfo.InvariantCulture)
            );
        }

        [Test]
        public void TestEmptyString()
        {
            var bindable = new Bindable<TestEnum>();
            var nullable = new Bindable<TestEnum?>();

            Assert.Throws<ArgumentException>(() =>
                bindable.Parse(string.Empty, CultureInfo.InvariantCulture)
            );
            nullable.Parse(string.Empty, CultureInfo.InvariantCulture);

            Assert.That(nullable.Value, Is.Null);
        }

        public enum TestEnum
        {
            Value1 = 0,
            Value2 = 1,
        }
    }
}
