using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DrawingRegister.App
{
    public partial class RescanFolderDialog : Window
    {
        private readonly List<string> _folderPaths;

        public string SelectedFolderPath { get; private set; } = string.Empty;

        public RescanFolderDialog(List<string> folderPaths)
        {
            InitializeComponent();

            _folderPaths = folderPaths;

            foreach (var path in folderPaths)
            {
                var item = new ListBoxItem
                {
                    Content = Path.GetFileName(path),
                    ToolTip = path,
                    Tag = path
                };
                FolderListBox.Items.Add(item);
            }
        }

        private void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListBox.SelectedItem is ListBoxItem selected)
            {
                SelectedFolderPath = (string)selected.Tag;
                DialogResult = true;
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a folder to rescan.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void FolderListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FolderListBox.SelectedItem is ListBoxItem selected)
            {
                SelectedFolderPath = (string)selected.Tag;
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Header_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
    }
}
