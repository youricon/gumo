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
            if (plugin.InstallGameFromController(Game))
            {
                InvokeOnInstalled(
                    new GameInstalledEventArgs(
                        new GameInstallationData
                        {
                            InstallDirectory = Game.InstallDirectory,
                        }));
            }
        }
    }
}
