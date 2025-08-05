using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    public partial class ReportFilterDialog : Window
    {
        public enum ReportType
        {
            AllDocuments,
            FilteredByPurpose,
            Transmittal
        }

        public ReportType SelectedReportType { get; private set; } = ReportType.AllDocuments;
        public string? SelectedPurpose { get; private set; }
        public DateTime? SelectedDate { get; private set; }
        public bool IncludeDistribution { get; private set; }
        public bool GroupByPackage { get; private set; }

        private readonly List<DateTime> _availableDates;

        public ReportFilterDialog(List<DateTime> availableDates)
        {
            InitializeComponent();
            _availableDates = availableDates;
            PopulateDateComboBox();
            
            // Set initial state after all components are loaded
            Loaded += ReportFilterDialog_Loaded;
        }

        private void ReportFilterDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial state programmatically to avoid null reference exceptions
            AllDocumentsRadio.IsChecked = true;
            SelectedReportType = ReportType.AllDocuments;
            PurposeFilterPanel.Visibility = Visibility.Collapsed;
            DateFilterPanel.Visibility = Visibility.Collapsed;
        }

        private void PopulateDateComboBox()
        {
            DateComboBox.Items.Clear();
            
            foreach (var date in _availableDates.OrderByDescending(d => d))
            {
                var item = new ComboBoxItem
                {
                    Content = date.ToString("dd/MM/yyyy"),
                    Tag = date
                };
                DateComboBox.Items.Add(item);
            }

            if (DateComboBox.Items.Count > 0)
            {
                DateComboBox.SelectedIndex = 0;
            }
        }

        private void ReportType_Changed(object sender, RoutedEventArgs e)
        {
            // Ensure all UI elements are loaded before trying to access them
            if (PurposeFilterPanel == null || DateFilterPanel == null || IncludeDistributionCheckBox == null)
                return;

            if (AllDocumentsRadio?.IsChecked == true)
            {
                SelectedReportType = ReportType.AllDocuments;
                PurposeFilterPanel.Visibility = Visibility.Collapsed;
                DateFilterPanel.Visibility = Visibility.Collapsed;
            }
            else if (FilteredDocumentsRadio?.IsChecked == true)
            {
                SelectedReportType = ReportType.FilteredByPurpose;
                PurposeFilterPanel.Visibility = Visibility.Visible;
                DateFilterPanel.Visibility = Visibility.Collapsed;
            }
            else if (TransmittalRadio?.IsChecked == true)
            {
                SelectedReportType = ReportType.Transmittal;
                PurposeFilterPanel.Visibility = Visibility.Collapsed;
                DateFilterPanel.Visibility = Visibility.Visible;
                IncludeDistributionCheckBox.IsChecked = true;
            }
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            IncludeDistribution = IncludeDistributionCheckBox.IsChecked ?? false;
            GroupByPackage = GroupByPackageCheckBox.IsChecked ?? false;

            if (SelectedReportType == ReportType.FilteredByPurpose)
            {
                var selectedItem = PurposeComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem != null && selectedItem.Content.ToString() != "All Purposes")
                {
                    SelectedPurpose = selectedItem.Content.ToString();
                }
            }
            else if (SelectedReportType == ReportType.Transmittal)
            {
                var selectedItem = DateComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is DateTime date)
                {
                    SelectedDate = date;
                }
            }

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