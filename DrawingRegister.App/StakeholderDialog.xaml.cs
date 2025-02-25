using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    public partial class StakeholderDialog : Window
    {
        public StakeholderInfo Stakeholder { get; private set; }
        public bool IsEditMode { get; private set; }

        public StakeholderDialog()
        {
            InitializeComponent();
            Stakeholder = new StakeholderInfo();
            IsEditMode = false;
        }

        public StakeholderDialog(StakeholderInfo stakeholder)
        {
            InitializeComponent();
            Stakeholder = stakeholder;
            IsEditMode = true;
            
            // Populate fields with existing data
            NameTextBox.Text = stakeholder.Name;
            CompanyTextBox.Text = stakeholder.Company;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a name for the stakeholder.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            // Update stakeholder info
            Stakeholder.Name = NameTextBox.Text.Trim();
            Stakeholder.Company = CompanyTextBox.Text.Trim();

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