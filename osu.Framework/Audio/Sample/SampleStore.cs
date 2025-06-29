﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio.Mixing;
using osu.Framework.Audio.Mixing.Bass;
using osu.Framework.IO.Stores;
using osu.Framework.Statistics;

namespace osu.Framework.Audio.Sample
{
    internal class SampleStore : AudioCollectionManager<AdjustableAudioComponent>, ISampleStore
    {
        private readonly ResourceStore<byte[]> store;
        private readonly AudioMixer mixer;

        private readonly Dictionary<string, SampleBassFactory> factories =
            new Dictionary<string, SampleBassFactory>();

        public int PlaybackConcurrency { get; set; } = Sample.DEFAULT_CONCURRENCY;

        internal SampleStore([NotNull] IResourceStore<byte[]> store, [NotNull] AudioMixer mixer)
        {
            this.store = new ResourceStore<byte[]>(store);
            this.mixer = mixer;

            AddExtension(@"wav");
            AddExtension(@"mp3");
        }

        public void AddExtension(string extension) => store.AddExtension(extension);

        public Sample Get(string name)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (string.IsNullOrEmpty(name))
                return null;

            lock (factories)
            {
                if (!factories.TryGetValue(name, out SampleBassFactory factory))
                {
                    this.LogIfNonBackgroundThread(name);

                    byte[] data = store.Get(name);
                    factory = factories[name] =
                        data == null
                            ? null
                            : new SampleBassFactory(data, name, (BassAudioMixer)mixer)
                            {
                                PlaybackConcurrency = { Value = PlaybackConcurrency },
                            };

                    if (factory != null)
                        AddItem(factory);
                }

                return factory?.CreateSample();
            }
        }

        public Task<Sample> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.Run(() => Get(name), cancellationToken);

        protected override void UpdateState()
        {
            FrameStatistics.Add(StatisticsCounterType.Samples, factories.Count);
            base.UpdateState();
        }

        public Stream GetStream(string name) => store.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => store.GetAvailableResources();
    }
}
