using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    public partial class CompanyDialog : Window
    {
        public DistributionCompany Company { get; private set; }
        public bool IsEditMode { get; private set; }

        public CompanyDialog()
        {
            InitializeComponent();
            Company = new DistributionCompany();
            IsEditMode = false;
        }

        public CompanyDialog(DistributionCompany company)
        {
            InitializeComponent();
            Company = company;
            IsEditMode = true;
            
            // Populate fields with existing data
            NameTextBox.Text = company.Name;
            
            // Set the category in the combobox
            bool categoryFound = false;
            foreach (ComboBoxItem item in CategoryComboBox.Items)
            {
                if (item.Content.ToString() == company.Category)
                {
                    CategoryComboBox.SelectedItem = item;
                    categoryFound = true;
                    break;
                }
            }
            
            // If category not found in predefined list, set it as text
            if (!categoryFound && !string.IsNullOrEmpty(company.Category))
            {
                CategoryComboBox.Text = company.Category;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a name for the company.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(CategoryComboBox.Text))
            {
                System.Windows.MessageBox.Show("Please select or enter a category.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryComboBox.Focus();
                return;
            }

            // Update company info
            Company.Name = NameTextBox.Text.Trim();
            Company.Category = CategoryComboBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Header_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
    }
} 