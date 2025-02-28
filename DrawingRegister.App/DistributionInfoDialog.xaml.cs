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
    /// Interaction logic for DistributionInfoDialog.xaml
    /// </summary>
    public partial class DistributionInfoDialog : Window
    {
        private readonly ProjectManager _project;
        private readonly DateTime _selectedDate;
        
        public DistributionInfoDialog(ProjectManager project, DateTime selectedDate)
        {
            InitializeComponent();
            
            _project = project;
            _selectedDate = selectedDate;
            
            // Set the date in the UI
            DateOfIssueText.Text = selectedDate.ToString("dd/MM/yyyy");
            
            // Load distribution information
            LoadDistributionInfo();
        }
        
        private void LoadDistributionInfo()
        {
            // Clear the panel (remove example items)
            DistributionPanel.Children.Clear();
            
            // Get all documents distributed on this date
            var docsForDate = _project.Documents
                .Where(d => d.DistributionCompanyIds.ContainsKey(_selectedDate))
                .ToList();
                
            if (!docsForDate.Any())
            {
                DistributionPanel.Children.Add(new TextBlock
                {
                    Text = "No distribution information found for this date.",
                    FontSize = 14,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                
                TotalRecipientsText.Text = "0";
                TotalDocumentsText.Text = "0";
                return;
            }
            
            // Get all company IDs that received documents on this date
            var allCompanyIds = docsForDate
                .SelectMany(d => d.DistributionCompanyIds.TryGetValue(_selectedDate, out var ids) ? ids : new List<string>())
                .Distinct()
                .ToList();
                
            // Get the actual company objects
            var companies = allCompanyIds
                .Select(id => _project.DistributionManager.Companies.FirstOrDefault(c => c.Id == id))
                .Where(c => c != null)
                .ToList();
                
            // Group companies by category
            var companiesByCategory = companies
                .GroupBy(c => c.Category)
                .OrderBy(g => g.Key)
                .ToList();
                
            int totalRecipients = 0;
            
            // Add each category and its companies
            foreach (var categoryGroup in companiesByCategory)
            {
                // Add category header
                DistributionPanel.Children.Add(new TextBlock
                {
                    Text = categoryGroup.Key,
                    Style = (Style)FindResource("CategoryHeaderStyle")
                });
                
                // Add each company in this category
                foreach (var company in categoryGroup.OrderBy(c => c.Name))
                {
                    // Count how many documents were distributed to this company
                    int documentCount = docsForDate.Count(d => 
                        d.DistributionCompanyIds.TryGetValue(_selectedDate, out var ids) && 
                        ids.Contains(company.Id));
                    
                    // Create grid for this company
                    var grid = new Grid { Style = (Style)FindResource("DistributionItemStyle") };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    // Company name
                    var nameText = new TextBlock
                    {
                        Text = company.Name,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(nameText, 0);
                    grid.Children.Add(nameText);
                    
                    // Document count
                    var countText = new TextBlock
                    {
                        Text = $"{documentCount} {(documentCount == 1 ? "document" : "documents")}",
                        FontSize = 14,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(countText, 1);
                    grid.Children.Add(countText);
                    
                    // Add to panel
                    DistributionPanel.Children.Add(grid);
                    
                    totalRecipients++;
                }
            }
            
            // Update summary information
            TotalRecipientsText.Text = totalRecipients.ToString();
            TotalDocumentsText.Text = docsForDate.Count.ToString();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
} 