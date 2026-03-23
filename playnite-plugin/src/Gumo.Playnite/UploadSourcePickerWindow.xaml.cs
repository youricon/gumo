using System.Windows;

namespace Gumo.Playnite
{
    public partial class UploadSourcePickerWindow : Window
    {
        public UploadSourcePickerWindow()
        {
            InitializeComponent();
        }

        public UploadSourcePickerSelection Selection { get; private set; } = UploadSourcePickerSelection.None;

        private void OnFileClick(object sender, RoutedEventArgs e)
        {
            Selection = UploadSourcePickerSelection.File;
            DialogResult = true;
        }

        private void OnFolderClick(object sender, RoutedEventArgs e)
        {
            Selection = UploadSourcePickerSelection.Folder;
            DialogResult = true;
        }
    }

    public enum UploadSourcePickerSelection
    {
        None = 0,
        File = 1,
        Folder = 2,
    }
}
