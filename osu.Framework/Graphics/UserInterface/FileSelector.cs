// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Input.Events;
using osu.Framework.Platform;

namespace osu.Framework.Graphics.UserInterface
{
    /// <summary>
    /// A component which allows a user to select a file.
    /// </summary>
    public abstract partial class FileSelector : DirectorySelector
    {
        private readonly string[] validFileExtensions;
        protected abstract DirectoryListingFile CreateFileItem(FileInfo file);

        [Cached]
        public readonly Bindable<FileInfo> CurrentFile = new Bindable<FileInfo>();

        [CanBeNull]
        private ISystemFileSelector systemFileSelector;

        public bool UsingSystemFileSelector => systemFileSelector != null;

        protected FileSelector(string initialPath = null, string[] validFileExtensions = null)
            : base(initialPath)
        {
            this.validFileExtensions = validFileExtensions ?? Array.Empty<string>();
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            bool presented = presentSystemSelectorIfAvailable(host);

            if (presented)
                TopLevelContent.Hide();
        }

        private bool presentSystemSelectorIfAvailable(GameHost host)
        {
            systemFileSelector = host.CreateSystemFileSelector(validFileExtensions);
            if (systemFileSelector == null)
                return false;

            systemFileSelector.Selected += f =>
                Schedule(() =>
                {
                    CurrentFile.Value = f;
                    CurrentPath.Value = f.Directory;
                });

            systemFileSelector.Present();
            return true;
        }

        protected override bool TryGetEntriesForPath(
            DirectoryInfo path,
            out ICollection<DirectorySelectorItem> items
        )
        {
            items = new List<DirectorySelectorItem>();

            if (!base.TryGetEntriesForPath(path, out var directories))
                return false;

            items = directories;

            try
            {
                IEnumerable<FileInfo> files = path.GetFiles();

                if (validFileExtensions.Length > 0)
                    files = files.Where(f => validFileExtensions.Contains(f.Extension));

                foreach (var file in files.OrderBy(d => d.Name))
                {
                    if (
                        ShowHiddenItems.Value || !file.Attributes.HasFlagFast(FileAttributes.Hidden)
                    )
                        items.Add(CreateFileItem(file));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            systemFileSelector?.Dispose();
            base.Dispose(isDisposing);
        }

        protected abstract partial class DirectoryListingFile : DirectorySelectorItem
        {
            protected readonly FileInfo File;

            [Resolved]
            private Bindable<FileInfo> currentFile { get; set; }

            protected DirectoryListingFile(FileInfo file)
            {
                File = file;

                try
                {
                    if (File?.Attributes.HasFlagFast(FileAttributes.Hidden) == true)
                        ApplyHiddenState();
                }
                catch (UnauthorizedAccessException)
                {
                    // checking attributes on access-controlled files will throw an error so we handle it here to prevent a crash
                }
            }

            protected override bool OnClick(ClickEvent e)
            {
                currentFile.Value = File;
                return true;
            }

            protected override string FallbackName => File.Name;
        }
    }
}
