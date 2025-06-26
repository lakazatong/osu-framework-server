using FlappyDon.Game.Elements;
using NUnit.Framework;
using osu.Framework.Allocation;

namespace FlappyDon.Game.Tests.Visual
{
    /// <summary>
    /// A test scene for testing the alignment
    /// and placement of the sprites that make up the backdrop
    /// </summary>
    [TestFixture]
    public partial class TestSceneBackdrop : FlappyDonTestScene
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Backdrop(() => new BackdropSprite()));
        }
    }
}
