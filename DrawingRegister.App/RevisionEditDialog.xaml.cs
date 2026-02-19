using System;
using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    public partial class RevisionEditDialog : Window
    {
        private readonly DocumentMetadata _document;
        private readonly bool _useNumericRevisions;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string IssuedBy { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;

        public RevisionEditDialog(DocumentMetadata document, DateTime issueDate, RevisionInfo revisionInfo, bool useNumericRevisions = false)
        {
            InitializeComponent();
            DataContext = this;

            _document = document;
            _useNumericRevisions = useNumericRevisions;
            DocumentNumber = document.DocumentNumber;
            IssueDate = issueDate;
            Purpose = revisionInfo.Purpose;
            Method = revisionInfo.Method;
            IssuedBy = revisionInfo.IssuedBy;
            Revision = revisionInfo.Revision;

            // Set initial combo box selections
            if (!string.IsNullOrEmpty(Purpose))
            {
                foreach (var item in PurposeCombo.Items)
                {
                    if (((ComboBoxItem)item).Content.ToString() == Purpose)
                    {
                        PurposeCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(Method))
            {
                foreach (var item in MethodCombo.Items)
                {
                    if (((ComboBoxItem)item).Content.ToString() == Method)
                    {
                        MethodCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            // Hook up purpose change event
            PurposeCombo.SelectionChanged += PurposeCombo_SelectionChanged;
        }

        private void PurposeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PurposeCombo.SelectedItem != null)
            {
                string selectedPurpose = ((ComboBoxItem)PurposeCombo.SelectedItem).Content.ToString()!;
                Revision = DocumentMetadata.GenerateRevisionCode(selectedPurpose, _document.RevisionHistory, _useNumericRevisions);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PurposeCombo.SelectedItem != null)
            {
                Purpose = ((ComboBoxItem)PurposeCombo.SelectedItem).Content.ToString()!;
                // Generate new revision code based on selected purpose
                Revision = DocumentMetadata.GenerateRevisionCode(Purpose, _document.RevisionHistory, _useNumericRevisions);
            }

            if (MethodCombo.SelectedItem != null)
            {
                Method = ((ComboBoxItem)MethodCombo.SelectedItem).Content.ToString()!;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
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