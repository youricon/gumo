using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Gumo.Playnite
{
    internal sealed class GumoUninstallController : UninstallController
    {
        private readonly GumoLibraryPlugin plugin;

        public GumoUninstallController(GumoLibraryPlugin plugin, Game game) : base(game)
        {
            this.plugin = plugin;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            plugin.UninstallGameFromController(Game);
            InvokeOnUninstalled();
        }
    }
}
