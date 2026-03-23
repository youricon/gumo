using System.Windows.Controls;

namespace Gumo.Playnite
{
    public partial class GumoLibrarySettingsView : UserControl
    {
        private readonly GumoLibraryPlugin plugin;

        public GumoLibrarySettingsView(GumoLibraryPlugin plugin, GumoLibrarySettings settings)
        {
            InitializeComponent();
            this.plugin = plugin;
            DataContext = settings;
        }

        private void OnTestConnectionClick(object sender, System.Windows.RoutedEventArgs e)
        {
            plugin.TestConnectionFromSettings();
        }
    }
}
