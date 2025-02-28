using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DrawingRegister.App.Models;

namespace DrawingRegister.App
{
    public partial class DocumentEditDialog : Window
    {
        private readonly DocumentMetadata _document;
        
        public string DocumentNumber { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public string Package { get; private set; } = string.Empty;
        public string DocumentType { get; private set; } = string.Empty;
        public string Size { get; private set; } = string.Empty;
        public string FilePath { get; private set; } = string.Empty;
        public bool UpdateFile => UpdateFileCheckbox.IsChecked == true;

        public DocumentEditDialog(DocumentMetadata document)
        {
            InitializeComponent();
            
            _document = document;
            
            // Initialize fields with current values
            DocumentNumberBox.Text = document.DocumentNumber;
            DescriptionBox.Text = document.Description;
            PackageBox.Text = document.Package;
            DocumentTypeBox.Text = document.DocumentType;
            SizeBox.Text = document.Size;
            FilePathBox.Text = document.FilePath;
            
            // Set up event handlers for preview updates
            DocumentNumberBox.TextChanged += (s, e) => UpdateFileNamePreview(s, e);
            DescriptionBox.TextChanged += (s, e) => UpdateFileNamePreview(s, e);
            PackageBox.TextChanged += (s, e) => UpdateFileNamePreview(s, e);
            DocumentTypeBox.TextChanged += (s, e) => UpdateFileNamePreview(s, e);
            SizeBox.TextChanged += (s, e) => UpdateFileNamePreview(s, e);
            UpdateFileCheckbox.Checked += (s, e) => UpdateFileNamePreview(null, null);
            UpdateFileCheckbox.Unchecked += (s, e) => UpdateFileNamePreview(null, null);
            
            // Initial preview update
            UpdateFileNamePreview(null, null);
        }

        private void UpdateFileNamePreview(object? sender, TextChangedEventArgs? e)
        {
            if (UpdateFileCheckbox.IsChecked != true)
            {
                FileNamePreview.Text = "File will not be renamed";
                return;
            }
            
            try
            {
                string originalPath = _document.FilePath;
                string? directory = Path.GetDirectoryName(originalPath);
                string extension = Path.GetExtension(originalPath);
                
                string newDocNumber = DocumentNumberBox.Text.Trim();
                string newDescription = DescriptionBox.Text.Trim();
                
                // Generate new filename based on project conventions
                string newFilename = FormatFileName(newDocNumber, newDescription);
                
                if (directory != null)
                {
                    string newPath = Path.Combine(directory, newFilename + extension);
                    
                    FileNamePreview.Text = newFilename + extension;
                    
                    // Check if target already exists
                    if (File.Exists(newPath) && !string.Equals(originalPath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        FileNamePreview.Text += "\n\nWARNING: A file with this name already exists!";
                    }
                }
                else
                {
                    FileNamePreview.Text = "Error: Could not determine directory path";
                }
            }
            catch (Exception ex)
            {
                FileNamePreview.Text = $"Error generating preview: {ex.Message}";
            }
        }
        
        private string FormatFileName(string docNumber, string description)
        {
            // Format according to naming convention
            // Replace spaces with underscores and remove invalid characters
            string formattedDescription = description
                .Replace(" ", "_")
                .Replace(".", "_")
                .Replace(",", "_")
                .Replace(":", "_")
                .Replace(";", "_")
                .Replace("/", "_")
                .Replace("\\", "_");
                
            return $"{docNumber}-{formattedDescription}";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(DocumentNumberBox.Text))
            {
                System.Windows.MessageBox.Show("Document Number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DocumentNumberBox.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
            {
                System.Windows.MessageBox.Show("Description is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DescriptionBox.Focus();
                return;
            }
            
            // Check for file rename conflicts
            if (UpdateFileCheckbox.IsChecked == true)
            {
                string originalPath = _document.FilePath;
                string? directory = Path.GetDirectoryName(originalPath);
                string extension = Path.GetExtension(originalPath);
                
                string newDocNumber = DocumentNumberBox.Text.Trim();
                string newDescription = DescriptionBox.Text.Trim();
                
                string newFilename = FormatFileName(newDocNumber, newDescription);
                
                if (directory != null)
                {
                    string newPath = Path.Combine(directory, newFilename + extension);
                    
                    if (File.Exists(newPath) && !string.Equals(originalPath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var result = System.Windows.MessageBox.Show(
                            "A file with this name already exists. Do you want to continue anyway?",
                            "File Already Exists",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                            
                        if (result == MessageBoxResult.No)
                            return;
                    }
                }
            }
            
            // Store values
            DocumentNumber = DocumentNumberBox.Text.Trim();
            Description = DescriptionBox.Text.Trim();
            Package = PackageBox.Text.Trim();
            DocumentType = DocumentTypeBox.Text.Trim();
            Size = SizeBox.Text.Trim();
            FilePath = _document.FilePath;
            
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 