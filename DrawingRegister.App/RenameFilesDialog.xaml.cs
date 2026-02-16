using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    public partial class RenameFilesDialog : Window
    {
        public ObservableCollection<RenameDisplayItem> RenameItems { get; } = new();
        public List<FileRenameInfo> ApprovedRenames { get; private set; } = new();

        public RenameFilesDialog(List<FileRenameInfo> suggestedRenames)
        {
            InitializeComponent();

            foreach (var rename in suggestedRenames)
            {
                RenameItems.Add(new RenameDisplayItem
                {
                    CurrentName = Path.GetFileName(rename.OriginalPath),
                    SuggestedName = rename.SuggestedName,
                    IsSelected = true,
                    RenameInfo = rename
                });
            }

            RenameListView.ItemsSource = RenameItems;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RenameItems)
                item.IsSelected = true;
            RenameListView.Items.Refresh();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RenameItems)
                item.IsSelected = false;
            RenameListView.Items.Refresh();
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            ApprovedRenames = RenameItems
                .Where(item => item.IsSelected)
                .Select(item => item.RenameInfo)
                .ToList();

            DialogResult = true;
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    public class RenameDisplayItem
    {
        public string CurrentName { get; set; } = string.Empty;
        public string SuggestedName { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
        public FileRenameInfo RenameInfo { get; set; } = new();
    }
}
