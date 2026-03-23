using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Gumo.Playnite
{
    public partial class OptionListPickerWindow : UserControl
    {
        private readonly Window hostWindow;

        public OptionListPickerWindow(
            Window hostWindow,
            string promptText,
            IEnumerable<OptionListPickerItem> items,
            OptionListPickerItem selectedItem = null)
        {
            InitializeComponent();
            this.hostWindow = hostWindow;
            PromptText = promptText;
            Items = items?.ToList() ?? new List<OptionListPickerItem>();
            SelectedItem = selectedItem ?? Items.FirstOrDefault();
            DataContext = this;
        }

        public string PromptText { get; }

        public List<OptionListPickerItem> Items { get; }

        public OptionListPickerItem SelectedItem { get; set; }

        private void OnAcceptClick(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                return;
            }

            hostWindow.DialogResult = true;
        }

        private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedItem == null)
            {
                return;
            }

            hostWindow.DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            hostWindow.DialogResult = false;
        }
    }

    public sealed class OptionListPickerItem
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public object Value { get; set; }
    }
}
