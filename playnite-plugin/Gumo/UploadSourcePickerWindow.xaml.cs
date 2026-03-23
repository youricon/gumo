using System.Windows;
using System.Windows.Controls;

namespace Gumo.Playnite
{
    public partial class UploadSourcePickerWindow : UserControl
    {
        private readonly Window hostWindow;

        public UploadSourcePickerWindow(Window hostWindow)
        {
            InitializeComponent();
            this.hostWindow = hostWindow;
        }

        public UploadSourcePickerSelection Selection { get; private set; } = UploadSourcePickerSelection.None;

        private void OnFileClick(object sender, RoutedEventArgs e)
        {
            Selection = UploadSourcePickerSelection.File;
            hostWindow.DialogResult = true;
        }

        private void OnFolderClick(object sender, RoutedEventArgs e)
        {
            Selection = UploadSourcePickerSelection.Folder;
            hostWindow.DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Selection = UploadSourcePickerSelection.None;
            hostWindow.DialogResult = false;
        }
    }

    public enum UploadSourcePickerSelection
    {
        None = 0,
        File = 1,
        Folder = 2,
    }
}
