using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private ProjectManager _projectManager;
        private ObservableCollection<DistributionCompany> _companies;
        private Dictionary<DateTime, List<string>> _companyDistributions = new();

        public DistributionDialog(DocumentMetadata document, ProjectManager projectManager)
        {
            InitializeComponent();
            _document = document;
            _projectManager = projectManager;
            DataContext = _document;
            
            // Get all issue dates from revision history
            _issueDates = _document.RevisionHistory.Keys.OrderBy(d => d).ToList();
            
            // Initialize company distributions from document
            _companies = _projectManager.DistributionManager.Companies;
            
            // Make a copy of the distribution data to allow cancellation
            foreach (var kvp in _document.DistributionCompanyIds)
            {
                _companyDistributions[kvp.Key] = new List<string>(kvp.Value);
            }
            
            // Set up the companies list with grouping by category
            SetupCompaniesList();
            
            // Build the distribution matrix
            BuildDistributionMatrix();
        }
        
        private void SetupCompaniesList()
        {
            // Create a CollectionViewSource for grouping
            var cvs = new CollectionViewSource();
            cvs.Source = _companies;
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            
            // Set the ListView's ItemsSource to the grouped view
            CompaniesList.ItemsSource = cvs.View;
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
            
            // Add the company header cell
            var headerCell = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#eb1845") as System.Windows.Media.Color? ?? Colors.Red),
                Padding = new Thickness(10, 5, 10, 5)
            };
            headerCell.Child = new TextBlock
            {
                Text = "Company",
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
            
            // Group companies by category
            var companiesByCategory = _companies
                .GroupBy(c => c.Category)
                .OrderBy(g => g.Key)
                .ToList();
                
            // Add category headers and rows for each company
            int rowIndex = 1;
            
            foreach (var categoryGroup in companiesByCategory)
            {
                // Add category header row
                DistributionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var categoryCell = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#f0f0f0") as System.Windows.Media.Color? ?? Colors.LightGray),
                    Padding = new Thickness(10, 5, 10, 5),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD") as System.Windows.Media.Color? ?? Colors.LightGray),
                    BorderThickness = new Thickness(1)
                };
                categoryCell.Child = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold
                };
                Grid.SetRow(categoryCell, rowIndex);
                Grid.SetColumn(categoryCell, 0);
                Grid.SetColumnSpan(categoryCell, _issueDates.Count + 1);
                DistributionGrid.Children.Add(categoryCell);
                
                rowIndex++;
                
                // Add rows for each company in this category
                foreach (var company in categoryGroup.OrderBy(c => c.Name))
                {
                    // Add row definition
                    DistributionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    // Add company name cell
                    var nameCell = new Border
                    {
                        BorderBrush = new SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD") as System.Windows.Media.Color? ?? Colors.LightGray),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10, 5, 10, 5)
                    };
                    nameCell.Child = new TextBlock
                    {
                        Text = company.Name,
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
                        
                        bool isDistributed = false;
                        DateTime issueDate = _issueDates[i];
                        if (_companyDistributions.TryGetValue(issueDate, out var companyIds))
                        {
                            isDistributed = companyIds.Contains(company.Id);
                        }
                        
                        var checkbox = new System.Windows.Controls.CheckBox
                        {
                            IsChecked = isDistributed,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Tag = new Tuple<string, DateTime>(company.Id, _issueDates[i])
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
        }
        
        private void Distribution_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkbox && checkbox.Tag is Tuple<string, DateTime> tag)
            {
                string companyId = tag.Item1;
                DateTime issueDate = tag.Item2;
                
                if (!_companyDistributions.ContainsKey(issueDate))
                {
                    _companyDistributions[issueDate] = new List<string>();
                }
                
                var companyIds = _companyDistributions[issueDate];
                
                if (checkbox.IsChecked == true)
                {
                    if (!companyIds.Contains(companyId))
                    {
                        companyIds.Add(companyId);
                    }
                }
                else
                {
                    companyIds.Remove(companyId);
                }
            }
        }
        
        private void AddCompany_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CompanyDialog();
            if (dialog.ShowDialog() == true)
            {
                // Add the new company
                _projectManager.DistributionManager.AddCompany(dialog.Company);
                
                // Refresh the UI
                SetupCompaniesList();
                BuildDistributionMatrix();
            }
        }
        
        private void EditCompany_Click(object sender, RoutedEventArgs e)
        {
            if (CompaniesList.SelectedItem is DistributionCompany selectedCompany)
            {
                var dialog = new CompanyDialog(selectedCompany);
                if (dialog.ShowDialog() == true)
                {
                    // Update the company info
                    _projectManager.DistributionManager.UpdateCompany(dialog.Company);
                    
                    // Refresh the UI
                    SetupCompaniesList();
                    BuildDistributionMatrix();
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a company to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void RemoveCompany_Click(object sender, RoutedEventArgs e)
        {
            if (CompaniesList.SelectedItem is DistributionCompany selectedCompany)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to remove '{selectedCompany.Name}'? This will remove it from all distributions.",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    // Remove the company
                    _projectManager.DistributionManager.RemoveCompany(selectedCompany);
                    
                    // Remove from all distributions
                    foreach (var issueDate in _companyDistributions.Keys.ToList())
                    {
                        _companyDistributions[issueDate].Remove(selectedCompany.Id);
                    }
                    
                    // Refresh the UI
                    SetupCompaniesList();
                    BuildDistributionMatrix();
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a company to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Update the document with the new distribution data
            _document.DistributionCompanyIds = new Dictionary<DateTime, List<string>>(_companyDistributions);
            
            // Save the project data
            _projectManager.SaveProjectData();
            
            DialogResult = true;
            Close();
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 