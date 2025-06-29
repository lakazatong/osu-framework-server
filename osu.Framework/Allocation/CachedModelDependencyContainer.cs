// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Extensions.TypeExtensions;

namespace osu.Framework.Allocation
{
    /// <summary>
    /// A <see cref="DependencyContainer"/> which caches a <typeparamref name="TModel"/> and members it contains attached with a <see cref="CachedAttribute"/> as dependencies.
    /// </summary>
    /// <remarks>
    /// Users can query the model by directly querying for the <typeparamref name="TModel"/> type,
    /// and query for the model's dependencies by providing the <typeparamref name="TModel"/> type as a parent.
    /// </remarks>
    /// <typeparam name="TModel">The type of the model to cache. Must contain only <see cref="Bindable{T}"/> fields or auto-properties.</typeparam>
    public class CachedModelDependencyContainer<TModel> : IReadOnlyDependencyContainer
        where TModel : class?, IDependencyInjectionCandidate?, new()
    {
        private const BindingFlags activator_flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly;

        /// <summary>
        /// The <typeparamref name="TModel"/> that provides the cached values.
        /// </summary>
        /// <remarks>
        /// This model is not injected directly, users of the <see cref="CachedModelDependencyContainer{TModel}"/> receive a shadow-bound copy of this value in all cases.
        /// </remarks>
        public readonly Bindable<TModel> Model = new Bindable<TModel>();

        private readonly TModel shadowModel = new TModel();
        private readonly IReadOnlyDependencyContainer? parent;
        private readonly IReadOnlyDependencyContainer shadowDependencies;

        public CachedModelDependencyContainer(IReadOnlyDependencyContainer? parent)
        {
            this.parent = parent;

            shadowDependencies = DependencyActivator.MergeDependencies(
                shadowModel,
                null,
                new CacheInfo(parent: typeof(TModel))
            );

            TModel? currentModel = null;
            Model.BindValueChanged(e =>
            {
                // When setting a null model, we actually want to reset the shadow model to a default state
                // rather than leaving the current state on-going
                var newModel = e.NewValue ?? new TModel();

                updateShadowModel(shadowModel, currentModel, newModel);

                currentModel = newModel;
            });
        }

        public object? Get(Type type) => Get(type, default);

        public object? Get(Type type, CacheInfo info)
        {
            if (info.Parent == null)
                return type == typeof(TModel) ? createChildShadowModel() : parent?.Get(type, info);
            if (info.Parent == typeof(TModel))
                return shadowDependencies.Get(type, info) ?? parent?.Get(type, info);

            return parent?.Get(type, info);
        }

        public void Inject<T>(T instance)
            where T : class, IDependencyInjectionCandidate =>
            DependencyActivator.Activate(instance, this);

        /// <summary>
        /// Creates a new shadow model bound to <see cref="shadowModel"/>.
        /// </summary>
        private TModel createChildShadowModel()
        {
            var result = new TModel();
            updateShadowModel(result, default, shadowModel);
            return result;
        }

        /// <summary>
        /// Updates a shadow model by unbinding from a previous model and binding to a new model.
        /// </summary>
        /// <param name="targetShadowModel">The shadow model to update.</param>
        /// <param name="lastModel">The model to unbind from.</param>
        /// <param name="newModel">The model to bind to.</param>
        private void updateShadowModel(TModel targetShadowModel, TModel? lastModel, TModel newModel)
        {
            if (lastModel != null)
            {
                foreach (var type in typeof(TModel).EnumerateBaseTypes())
                {
                    foreach (var field in type.GetFields(activator_flags))
                        perform(
                            field,
                            targetShadowModel,
                            lastModel,
                            (shadowProp, modelProp) => shadowProp.UnbindFrom(modelProp)
                        );
                }
            }

            foreach (var type in typeof(TModel).EnumerateBaseTypes())
            {
                foreach (var field in type.GetFields(activator_flags))
                    perform(
                        field,
                        targetShadowModel,
                        newModel,
                        (shadowProp, modelProp) => shadowProp.BindTo(modelProp)
                    );
            }
        }

        /// <summary>
        /// Perform an arbitrary action across a shadow model and model.
        /// </summary>
        private static void perform(
            FieldInfo field,
            TModel shadowModel,
            TModel targetModel,
            Action<IBindable, IBindable> action
        )
        {
            IBindable? shadowBindable = null;
            IBindable? targetBindable = null;

            try
            {
                shadowBindable = field.GetValue(shadowModel) as IBindable;
                targetBindable = field.GetValue(targetModel) as IBindable;
            }
            catch { }

            if (shadowBindable != null && targetBindable != null)
                action(shadowBindable, targetBindable);
        }
    }
}
