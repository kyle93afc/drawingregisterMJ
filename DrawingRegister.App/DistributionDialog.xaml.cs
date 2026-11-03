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
using MessageBox = System.Windows.MessageBox;

namespace DrawingRegister.App
{
    /// <summary>
    /// Interaction logic for DistributionDialog.xaml
    /// </summary>
    public partial class DistributionDialog : Window
    {
        private ProjectManager _projectManager;
        private ObservableCollection<DistributionCompany> _companies;

        public DistributionDialog(ProjectManager projectManager)
        {
            InitializeComponent();
            _projectManager = projectManager;
            
            // Initialize company list
            _companies = _projectManager.DistributionManager.Companies;
            
            // Set up the companies list with grouping by category
            SetupCompaniesList();
            
            // Set window title
            Title = "Manage Distribution Companies";
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
        
        private void AddCompany_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CompanyDialog();
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                _projectManager.DistributionManager.AddCompany(dialog.Company);
                SetupCompaniesList(); // Refresh the list
            }
        }
        
        private void EditCompany_Click(object sender, RoutedEventArgs e)
        {
            if (CompaniesList.SelectedItem is DistributionCompany selectedCompany)
            {
                var dialog = new CompanyDialog(selectedCompany);
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true)
                {
                    _projectManager.DistributionManager.UpdateCompany(dialog.Company);
                    SetupCompaniesList(); // Refresh the list
                }
            }
            else
            {
                MessageBox.Show("Please select a company to edit.", "No Company Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void DeleteCompany_Click(object sender, RoutedEventArgs e)
        {
            if (CompaniesList.SelectedItem is DistributionCompany selectedCompany)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the company '{selectedCompany.Name}'?\n\nThis may affect document distributions.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result == MessageBoxResult.Yes)
                {
                    _projectManager.DistributionManager.RemoveCompany(selectedCompany);
                    SetupCompaniesList(); // Refresh the list
                }
            }
            else
            {
                MessageBox.Show("Please select a company to delete.", "No Company Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Header_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
    }
} 