using System.Windows.Controls;

namespace Gumo.Playnite
{
    public partial class GumoLibrarySettingsView : UserControl
    {
        public GumoLibrarySettingsView(GumoLibrarySettings settings)
        {
            InitializeComponent();
            DataContext = settings;
        }
    }
}
