using NUnit.Framework;
using osu.Framework.Allocation;

namespace FlappyDon.Game.Tests.Visual
{
    /// <summary>
    /// A test scene wrapping the entire game,
    /// including audio.
    /// </summary>
    [TestFixture]
    public partial class TestSceneFlappyDonGame : FlappyDonTestScene
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            AddGame(new FlappyDonGame());
        }
    }
}
