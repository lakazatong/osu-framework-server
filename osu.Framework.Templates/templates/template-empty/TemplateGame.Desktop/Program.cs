﻿using osu.Framework;
using osu.Framework.Platform;
using TemplateGame.Game;

namespace TemplateGame.Desktop
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost(@"TemplateGame"))
            using (osu.Framework.Game game = new TemplateGameGame())
                host.Run(game);
        }
    }
}
