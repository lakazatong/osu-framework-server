// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace osu.Framework.Tests.Bindables
{
    [TestFixture]
    public class BindableTest
    {
        /// <summary>
        /// Tests that a value provided in the constructor is used as the default value for the bindable.
        /// </summary>
        [Test]
        public void TestConstructorValueUsedAsDefaultValue()
        {
            Assert.That(new Bindable<int>(10).Default, Is.EqualTo(10));
        }

        /// <summary>
        /// Tests that a value provided in the constructor is used as the initial value for the bindable.
        /// </summary>
        [Test]
        public void TestConstructorValueUsedAsInitialValue()
        {
            Assert.That(new Bindable<int>(10).Value, Is.EqualTo(10));
        }

        /// <summary>
        /// Tests binding via the various <see cref="Bindable{T}.BindTarget"/> methods.
        /// </summary>
        [Test]
        public void TestBindViaBindTarget()
        {
            Bindable<int> parentBindable = new Bindable<int>();

            Bindable<int> bindable1 = new Bindable<int>();
            IBindable<int> bindable2 = new Bindable<int>();
            IBindable bindable3 = new Bindable<int>();

            bindable1.BindTarget = parentBindable;
            bindable2.BindTarget = parentBindable;
            bindable3.BindTarget = parentBindable;

            parentBindable.Value = 5;
            parentBindable.Disabled = true;

            Assert.That(bindable1.Value, Is.EqualTo(5));
            Assert.That(bindable2.Value, Is.EqualTo(5));
            Assert.That(bindable3.Disabled, Is.True); // Only have access to disabled
        }

        [Test]
        public void TestParseBindableOfExactSameType()
        {
            var bindable1 = new BindableInt();
            var bindable2 = new BindableDouble();
            var bindable3 = new BindableBool();
            var bindable4 = new Bindable<string>();

            bindable1.Parse(new BindableInt(3), CultureInfo.InvariantCulture);
            bindable2.Parse(new BindableDouble(2.5), CultureInfo.InvariantCulture);
            bindable3.Parse(new BindableBool(true), CultureInfo.InvariantCulture);
            bindable4.Parse(new Bindable<string>("string value"), CultureInfo.InvariantCulture);

            Assert.That(bindable1.Value, Is.EqualTo(3));
            Assert.That(bindable2.Value, Is.EqualTo(2.5));
            Assert.That(bindable3.Value, Is.EqualTo(true));
            Assert.That(bindable4.Value, Is.EqualTo("string value"));

            // parsing bindable of different type should throw exception.
            Assert.Throws<ArgumentException>(() =>
                bindable1.Parse(new BindableDouble(3.0), CultureInfo.InvariantCulture)
            );
        }

        [Test]
        public void TestParseBindableOfMatchingInterfaceType()
        {
            // both of these implement IBindable<int>
            var bindable1 = new BindableInt(10) { MaxValue = 15 };
            var bindable2 = new Bindable<int>(20);

            bindable1.Parse(bindable2, CultureInfo.InvariantCulture);
            // ensure MaxValue is still respected.
            Assert.That(bindable1.Value, Is.EqualTo(15));

            bindable2.Parse(bindable1, CultureInfo.InvariantCulture);
            Assert.That(bindable2.Value, Is.EqualTo(15));
        }

        [TestCaseSource(nameof(getParsingConversionTests))]
        public void TestParse(Type type, object input, object output)
        {
            object bindable = Activator.CreateInstance(
                typeof(Bindable<>).MakeGenericType(type),
                type == typeof(string) ? "" : Activator.CreateInstance(type)
            );
            Debug.Assert(bindable != null);

            ((IParseable)bindable).Parse(input, CultureInfo.InvariantCulture);
            object value = bindable
                .GetType()
                .GetProperty(
                    nameof(Bindable<object>.Value),
                    BindingFlags.Public | BindingFlags.Instance
                )
                ?.GetValue(bindable);

            Assert.That(value, Is.EqualTo(output));
        }

        // Bindable<int>.Parse(null, CultureInfo.InvariantCulture)
        [Test]
        public void TestParseNullIntoValueType()
        {
            Bindable<int> bindable = new Bindable<int>();
            Assert.That(
                () => bindable.Parse(null, CultureInfo.InvariantCulture),
                Throws.ArgumentNullException
            );
        }

        // Bindable<int>.Parse(string.Empty, CultureInfo.InvariantCulture)
        [Test]
        public void TestParseEmptyStringIntoValueType()
        {
            Bindable<int> bindable = new Bindable<int>();
            Assert.Throws<FormatException>(() =>
                bindable.Parse(string.Empty, CultureInfo.InvariantCulture)
            );
        }

        // Bindable<int?>.Parse(null, CultureInfo.InvariantCulture)
        [Test]
        public void TestParseNullIntoNullableValueType()
        {
            Bindable<int?> bindable = new Bindable<int?>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        // Bindable<int?>.Parse(string.Empty, CultureInfo.InvariantCulture)
        [Test]
        public void TestParseEmptyStringIntoNullableValueType()
        {
            Bindable<int?> bindable = new Bindable<int?>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        // Bindable<Class>.Parse(null, CultureInfo.InvariantCulture)
        [Test]
        public void TestParseNullIntoReferenceType()
        {
            Bindable<TestClass> bindable = new Bindable<TestClass>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        // Bindable<Class>.Parse(string.Empty, CultureInfo.InvariantCulture)
        [Test]
        public void TestParseEmptyStringIntoReferenceType()
        {
            Bindable<TestClass> bindable = new Bindable<TestClass>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

#nullable enable
        // Bindable<Class>.Parse(null, CultureInfo.InvariantCulture) -- NRT
        [Test]
        public void TestParseNullIntoReferenceTypeWithNRT()
        {
            Bindable<TestClass> bindable = new Bindable<TestClass>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        // Bindable<Class>.Parse(string.Empty, CultureInfo.InvariantCulture) -- NRT
        [Test]
        public void TestParseEmptyStringIntoReferenceTypeWithNRT()
        {
            Bindable<TestClass> bindable = new Bindable<TestClass>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        // Bindable<Class?>.Parse(null, CultureInfo.InvariantCulture) -- NRT
        [Test]
        public void TestParseNullIntoNullableReferenceTypeWithNRT()
        {
            Bindable<TestClass?> bindable = new Bindable<TestClass?>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        // Bindable<Class?>.Parse(string.Empty, CultureInfo.InvariantCulture) -- NRT
        [Test]
        public void TestParseEmptyStringIntoNullableReferenceTypeWithNRT()
        {
            Bindable<TestClass?> bindable = new Bindable<TestClass?>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

#nullable disable

        [Test]
        public void TestParseNullIntoStringType()
        {
            Bindable<string> bindable = new Bindable<string>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        [Test]
        public void TestParseEmptyStringIntoStringType()
        {
            Bindable<string> bindable = new Bindable<string>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Empty);
        }

#nullable enable
        [Test]
        public void TestParseNullIntoStringTypeWithNRT()
        {
            Bindable<string> bindable = new Bindable<string>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        [Test]
        public void TestParseEmptyStringIntoStringTypeWithNRT()
        {
            Bindable<string> bindable = new Bindable<string>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Empty);
        }

        [Test]
        public void TestParseNullIntoNullableStringTypeWithNRT()
        {
            Bindable<string?> bindable = new Bindable<string?>();
            bindable.Parse(null, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Null);
        }

        [Test]
        public void TestParseEmptyStringIntoNullableStringTypeWithNRT()
        {
            Bindable<string?> bindable = new Bindable<string?>();
            bindable.Parse(string.Empty, CultureInfo.InvariantCulture);
            Assert.That(bindable.Value, Is.Empty);
        }

#nullable disable

        private static IEnumerable<object[]> getParsingConversionTests()
        {
            var testTypes = new[]
            {
                typeof(bool),
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(float),
                typeof(double),
                typeof(short),
                typeof(ushort),
                typeof(byte),
                typeof(sbyte),
                typeof(decimal),
                typeof(string),
            };

            object[] inputs =
            {
                1,
                "1",
                1.0,
                1.0f,
                1L,
                1m,
                1.5,
                "1.5",
                1.5f,
                1.5m,
                -1,
                "-1",
                -1.0,
                -1.0f,
                -1L,
                -1m,
                -1.5,
                "-1.5",
                -1.5f,
                -1.5m,
            };

            foreach (var type in testTypes)
            {
                foreach (object input in inputs)
                {
                    object expectedOutput = null;

                    try
                    {
                        expectedOutput = Convert.ChangeType(
                            input,
                            type,
                            CultureInfo.InvariantCulture
                        );
                    }
                    catch
                    {
                        // Not worried about invalid conversions - they'll never work by the base bindable anyway
                    }

                    if (expectedOutput != null)
                        yield return new[] { type, input, expectedOutput };
                }
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class TestClass { }
    }
}
