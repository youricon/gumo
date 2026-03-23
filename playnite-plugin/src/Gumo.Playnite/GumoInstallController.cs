using System;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Gumo.Playnite
{
    internal sealed class GumoInstallController : InstallController
    {
        private readonly GumoLibraryPlugin plugin;

        public GumoInstallController(GumoLibraryPlugin plugin, Game game) : base(game)
        {
            this.plugin = plugin;
        }

        public override void Install(InstallActionArgs args)
        {
            plugin.InstallGameFromController(Game);
        }
    }
}
