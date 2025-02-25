using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    /// <summary>
    /// Interaction logic for DistributionDialog.xaml
    /// </summary>
    public partial class DistributionDialog : Window
    {
        private DocumentMetadata _document;
        private List<DateTime> _issueDates = new();
        private Dictionary<string, StakeholderInfo> _stakeholders = new();
        private Dictionary<string, StakeholderInfo> _originalStakeholders = new();

        public DistributionDialog(DocumentMetadata document)
        {
            InitializeComponent();
            _document = document;
            DataContext = _document;
            
            // Make a deep copy of the stakeholders to allow cancellation
            foreach (var kvp in _document.Stakeholders)
            {
                _originalStakeholders[kvp.Key] = new StakeholderInfo
                {
                    Name = kvp.Value.Name,
                    Company = kvp.Value.Company,
                    DistributionDates = new List<DateTime>(kvp.Value.DistributionDates)
                };
                
                _stakeholders[kvp.Key] = new StakeholderInfo
                {
                    Name = kvp.Value.Name,
                    Company = kvp.Value.Company,
                    DistributionDates = new List<DateTime>(kvp.Value.DistributionDates)
                };
            }
            
            // Get all issue dates from revision history
            _issueDates = _document.RevisionHistory.Keys.OrderBy(d => d).ToList();
            
            // Populate the stakeholders list
            RefreshStakeholdersList();
            
            // Build the distribution matrix
            BuildDistributionMatrix();
        }
        
        private void RefreshStakeholdersList()
        {
            StakeholdersList.Items.Clear();
            foreach (var stakeholder in _stakeholders.Values)
            {
                StakeholdersList.Items.Add(stakeholder);
            }
        }
        
        private void BuildDistributionMatrix()
        {
            // Clear existing grid
            DistributionGrid.Children.Clear();
            DistributionGrid.RowDefinitions.Clear();
            DistributionGrid.ColumnDefinitions.Clear();
            
            // Add the first row and column definitions
            DistributionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DistributionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // Add the stakeholder header cell
            var headerCell = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#eb1845") as System.Windows.Media.Color? ?? Colors.Red),
                Padding = new Thickness(10, 5, 10, 5)
            };
            headerCell.Child = new TextBlock
            {
                Text = "Stakeholder",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(headerCell, 0);
            Grid.SetColumn(headerCell, 0);
            DistributionGrid.Children.Add(headerCell);
            
            // Add column headers for each issue date
            for (int i = 0; i < _issueDates.Count; i++)
            {
                // Add column definition
                DistributionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                // Create header cell
                var dateCell = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#eb1845") as System.Windows.Media.Color? ?? Colors.Red),
                    Padding = new Thickness(10, 5, 10, 5),
                    BorderBrush = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(1, 0, 0, 0)
                };
                
                // Get revision for this date
                string revision = _document.RevisionHistory.ContainsKey(_issueDates[i]) 
                    ? _document.RevisionHistory[_issueDates[i]].Revision 
                    : "";
                
                // Create stacked text for date and revision
                var stackPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
                stackPanel.Children.Add(new TextBlock
                {
                    Text = _issueDates[i].ToString("dd/MM/yyyy"),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"Rev {revision}",
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                
                dateCell.Child = stackPanel;
                Grid.SetRow(dateCell, 0);
                Grid.SetColumn(dateCell, i + 1);
                DistributionGrid.Children.Add(dateCell);
            }
            
            // Add rows for each stakeholder
            int rowIndex = 1;
            foreach (var kvp in _stakeholders)
            {
                string stakeholderId = kvp.Key;
                StakeholderInfo stakeholder = kvp.Value;
                
                // Add row definition
                DistributionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // Add stakeholder name cell
                var nameCell = new Border
                {
                    BorderBrush = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD") as System.Windows.Media.Color? ?? Colors.LightGray),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 5, 10, 5)
                };
                nameCell.Child = new TextBlock
                {
                    Text = $"{stakeholder.Name} ({stakeholder.Company})",
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(nameCell, rowIndex);
                Grid.SetColumn(nameCell, 0);
                DistributionGrid.Children.Add(nameCell);
                
                // Add checkboxes for each issue date
                for (int i = 0; i < _issueDates.Count; i++)
                {
                    var checkboxCell = new Border
                    {
                        BorderBrush = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD") as System.Windows.Media.Color? ?? Colors.LightGray),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(5)
                    };
                    
                    var checkbox = new System.Windows.Controls.CheckBox
                    {
                        IsChecked = stakeholder.DistributionDates.Contains(_issueDates[i]),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = new Tuple<string, DateTime>(stakeholderId, _issueDates[i])
                    };
                    checkbox.Checked += Distribution_CheckChanged;
                    checkbox.Unchecked += Distribution_CheckChanged;
                    
                    checkboxCell.Child = checkbox;
                    Grid.SetRow(checkboxCell, rowIndex);
                    Grid.SetColumn(checkboxCell, i + 1);
                    DistributionGrid.Children.Add(checkboxCell);
                }
                
                rowIndex++;
            }
        }
        
        private void Distribution_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkbox && checkbox.Tag is Tuple<string, DateTime> tag)
            {
                string stakeholderId = tag.Item1;
                DateTime issueDate = tag.Item2;
                
                if (_stakeholders.TryGetValue(stakeholderId, out var stakeholder))
                {
                    if (checkbox.IsChecked == true)
                    {
                        if (!stakeholder.DistributionDates.Contains(issueDate))
                        {
                            stakeholder.DistributionDates.Add(issueDate);
                        }
                    }
                    else
                    {
                        stakeholder.DistributionDates.Remove(issueDate);
                    }
                }
            }
        }
        
        private void AddStakeholder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StakeholderDialog();
            if (dialog.ShowDialog() == true)
            {
                // Generate a unique ID for the new stakeholder
                string id = Guid.NewGuid().ToString();
                
                // Add the new stakeholder
                _stakeholders[id] = new StakeholderInfo
                {
                    Name = dialog.Stakeholder.Name,
                    Company = dialog.Stakeholder.Company,
                    DistributionDates = new List<DateTime>()
                };
                
                // Refresh the UI
                RefreshStakeholdersList();
                BuildDistributionMatrix();
            }
        }
        
        private void EditStakeholder_Click(object sender, RoutedEventArgs e)
        {
            if (StakeholdersList.SelectedItem is StakeholderInfo selectedStakeholder)
            {
                // Find the stakeholder ID
                string? stakeholderId = _stakeholders.FirstOrDefault(s => s.Value == selectedStakeholder).Key;
                if (stakeholderId == null) return;
                
                var dialog = new StakeholderDialog(selectedStakeholder);
                if (dialog.ShowDialog() == true)
                {
                    // Update the stakeholder info
                    _stakeholders[stakeholderId].Name = dialog.Stakeholder.Name;
                    _stakeholders[stakeholderId].Company = dialog.Stakeholder.Company;
                    
                    // Refresh the UI
                    RefreshStakeholdersList();
                    BuildDistributionMatrix();
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a stakeholder to edit.", "Selection Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        
        private void RemoveStakeholder_Click(object sender, RoutedEventArgs e)
        {
            if (StakeholdersList.SelectedItem is StakeholderInfo selectedStakeholder)
            {
                // Find the stakeholder ID
                string? stakeholderId = _stakeholders.FirstOrDefault(s => s.Value == selectedStakeholder).Key;
                if (stakeholderId == null) return;
                
                // Confirm deletion
                if (System.Windows.MessageBox.Show(
                    $"Are you sure you want to remove {selectedStakeholder.Name} from the distribution list?", 
                    "Confirm Removal", 
                    System.Windows.MessageBoxButton.YesNo, 
                    System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
                {
                    // Remove the stakeholder
                    _stakeholders.Remove(stakeholderId);
                    
                    // Refresh the UI
                    RefreshStakeholdersList();
                    BuildDistributionMatrix();
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a stakeholder to remove.", "Selection Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Update the document with the modified stakeholders
            _document.Stakeholders.Clear();
            foreach (var kvp in _stakeholders)
            {
                _document.Stakeholders[kvp.Key] = kvp.Value;
            }
            
            DialogResult = true;
            Close();
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore original stakeholders
            _document.Stakeholders.Clear();
            foreach (var kvp in _originalStakeholders)
            {
                _document.Stakeholders[kvp.Key] = kvp.Value;
            }
            
            DialogResult = false;
            Close();
        }
    }
} 