// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;

namespace osu.Framework.Tests.Dependencies.SourceGeneration
{
    [TestFixture]
    public partial class CachedModelDependenciesTest
    {
        [Test]
        public void TestSettingNoModelResolvesDefault()
        {
            var resolver = new FieldModelResolver();
            var dependencies = new CachedModelDependencyContainer<FieldModel>(null);

            dependencies.Inject(resolver);

            Assert.AreEqual(1, resolver.Model.Bindable.Value);
        }

        [Test]
        public void TestModelWithBindableFieldsPropagatesToChildren()
        {
            var resolver = new FieldModelResolver();
            var dependencies = new CachedModelDependencyContainer<FieldModel>(null)
            {
                Model = { Value = new FieldModel { Bindable = { Value = 2 } } },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Model.Bindable.Value);
        }

        [Test]
        public void TestModelWithBindablePropertiesPropagatesToChildren()
        {
            var resolver = new FieldModelResolver();
            var dependencies = new CachedModelDependencyContainer<FieldModel>(null)
            {
                Model = { Value = new FieldModel { Bindable = { Value = 2 } } },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Model.Bindable.Value);
        }

        [Test]
        public void TestChangeModelValuePropagatesToChildren()
        {
            var resolver = new FieldModelResolver();
            var dependencies = new CachedModelDependencyContainer<FieldModel>(null)
            {
                Model = { Value = new FieldModel { Bindable = { Value = 2 } } },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Model.Bindable.Value);

            dependencies.Model.Value.Bindable.Value = 3;

            Assert.AreEqual(3, resolver.Model.Bindable.Value);
        }

        [Test]
        public void TestSubClassedModelCachedAllSuperClasses()
        {
            var resolver = new DerivedFieldModelResolver();
            var dependencies = new CachedModelDependencyContainer<DerivedFieldModel>(null)
            {
                Model =
                {
                    Value = new DerivedFieldModel
                    {
                        Bindable = { Value = 2 },
                        BindableString = { Value = "2" },
                    },
                },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Model.Bindable.Value);
            Assert.AreEqual("2", resolver.Model.BindableString.Value);

            dependencies.Model.Value.Bindable.Value = 3;
            dependencies.Model.Value.BindableString.Value = "3";

            Assert.AreEqual(3, resolver.Model.Bindable.Value);
            Assert.AreEqual("3", resolver.Model.BindableString.Value);
        }

        [Test]
        public void TestCrossDependentBindsDoNotPollute()
        {
            var model1 = new CrossDependentFieldModel { Bindable = { Value = 2 } };
            var model2 = new CrossDependentFieldModel { Bindable = { Value = 3 } };

            var dependencies = new CachedModelDependencyContainer<CrossDependentFieldModel>(null)
            {
                Model = { Value = model1 },
            };

            Assert.AreEqual(2, model1.Bindable.Value);
            Assert.AreEqual(2, model1.BindableTwo.Value);

            Assert.AreEqual(3, model2.Bindable.Value);
            Assert.AreEqual(3, model2.BindableTwo.Value);

            dependencies.Model.Value = model2;

            Assert.AreEqual(2, model1.Bindable.Value);
            Assert.AreEqual(2, model1.BindableTwo.Value);

            Assert.AreEqual(3, model2.Bindable.Value);
            Assert.AreEqual(3, model2.BindableTwo.Value);
        }

        private partial class CrossDependentFieldModel : IDependencyInjectionCandidate
        {
            [Cached]
            public readonly Bindable<int> Bindable = new Bindable<int>(1);

            [Cached]
            public readonly Bindable<int> BindableTwo = new Bindable<int>();

            public CrossDependentFieldModel()
            {
                Bindable.BindValueChanged(e => BindableTwo.Value = e.NewValue);
            }
        }

        [Test]
        public void TestChangeModelPropagatesAllChanges()
        {
            var resolver = new FieldModelResolver();

            var model1 = new FieldModel { Bindable = { Value = 2 } };
            var model2 = new FieldModel { Bindable = { Value = 3 } };

            var dependencies = new CachedModelDependencyContainer<FieldModel>(null)
            {
                Model = { Value = model1 },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Model.Bindable.Value);

            dependencies.Model.Value = model2;

            Assert.AreEqual(3, resolver.Model.Bindable.Value);

            model1.Bindable.Value = 4;

            Assert.AreEqual(3, resolver.Model.Bindable.Value);

            model2.Bindable.Value = 4;

            Assert.AreEqual(4, resolver.Model.Bindable.Value);
        }

        [Test]
        public void TestSetModelToNullAfterResolved()
        {
            var resolver = new FieldModelResolver();

            var model = new FieldModel { Bindable = { Value = 2 } };

            var dependencies = new CachedModelDependencyContainer<FieldModel?>(null)
            {
                Model = { Value = model },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Model.Bindable.Value);

            dependencies.Model.Value = null;

            // Should be reset to the default value
            Assert.AreEqual(1, resolver.Model.Bindable.Value);

            model.Bindable.Value = 3;

            Assert.AreEqual(1, resolver.Model.Bindable.Value);
        }

        [Test]
        public void TestInjectionResolvesDifferingShadowModels()
        {
            var resolver1 = new FieldModelResolver();
            var resolver2 = new FieldModelResolver();

            var dependencies = new CachedModelDependencyContainer<FieldModel>(null)
            {
                Model = { Value = new FieldModel() },
            };

            dependencies.Inject(resolver1);
            dependencies.Inject(resolver2);

            Assert.AreNotSame(resolver1.Model, resolver2.Model);
        }

        [Test]
        public void TestResolveIndividualProperties()
        {
            var resolver = new DerivedFieldModelPropertyResolver();

            var model1 = new DerivedFieldModel
            {
                Bindable = { Value = 2 },
                BindableString = { Value = "2" },
            };

            var model2 = new DerivedFieldModel
            {
                Bindable = { Value = 3 },
                BindableString = { Value = "3" },
            };

            var dependencies = new CachedModelDependencyContainer<DerivedFieldModel?>(null)
            {
                Model = { Value = model1 },
            };

            dependencies.Inject(resolver);

            Assert.AreEqual(2, resolver.Bindable.Value);
            Assert.AreEqual("2", resolver.BindableString.Value);

            dependencies.Model.Value = model2;

            Assert.AreEqual(3, resolver.Bindable.Value);
            Assert.AreEqual("3", resolver.BindableString.Value);

            dependencies.Model.Value = null;

            Assert.AreEqual(1, resolver.Bindable.Value);
            Assert.AreEqual(null, resolver.BindableString.Value);
        }

        private partial class FieldModel : IDependencyInjectionCandidate
        {
            [Cached]
            public readonly Bindable<int> Bindable = new Bindable<int>(1);
        }

        private partial class DerivedFieldModel : FieldModel
        {
            [Cached]
            public readonly Bindable<string> BindableString = new Bindable<string>();
        }

        private partial class FieldModelResolver : IDependencyInjectionCandidate
        {
            [Resolved]
            public FieldModel Model { get; private set; } = null!;
        }

        private partial class DerivedFieldModelResolver : IDependencyInjectionCandidate
        {
            [Resolved]
            public DerivedFieldModel Model { get; private set; } = null!;
        }

        private partial class DerivedFieldModelPropertyResolver : IDependencyInjectionCandidate
        {
            [Resolved(typeof(DerivedFieldModel))]
            public Bindable<int> Bindable { get; private set; } = null!;

            [Resolved(typeof(DerivedFieldModel))]
            public Bindable<string> BindableString { get; private set; } = null!;
        }
    }
}
