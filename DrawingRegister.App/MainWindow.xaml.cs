using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Threading.Tasks;
using DrawingRegister.App.Models;
using System.Collections.Generic;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Binding = System.Windows.Data.Binding;
using System.Windows.Media;
using System.Collections.ObjectModel;

namespace DrawingRegister.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
{
    private readonly ProjectManager _project = new();
    private bool _disposed;
    private string _searchText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
            FilterDocuments();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Bind UI elements to ProjectManager properties
        ProjectNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ProjectNumber)) { Source = _project });
        ProjectNameBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ProjectName)) { Source = _project });
        DisciplineBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.Discipline)) { Source = _project });
        RegNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.RegisterNumber)) { Source = _project });
        ClientNoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(ProjectManager.ClientNumber)) { Source = _project });

        // Bind grid to ProjectManager collections
        DocumentGrid.ItemsSource = _project.Documents;
        
        // Add headers to revision rows
        RevisionDatesRow.Items.Add("Issue Date");
        RevisionPurposeRow.Items.Add("Purpose");
        RevisionMethodRow.Items.Add("Method");
        RevisionIssuedByRow.Items.Add("Issued By");
    }

    private void FilterDocuments()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(_project.Documents);
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            view.Filter = null;
            return;
        }

        view.Filter = obj =>
        {
            if (obj is not DocumentMetadata doc) return false;
            return doc.DocumentNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   doc.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   doc.Package.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void UpdateRevisionHistory()
    {
        // Clear existing items except headers
        RevisionDatesRow.Items.Clear();
        RevisionPurposeRow.Items.Clear();
        RevisionMethodRow.Items.Clear();
        RevisionIssuedByRow.Items.Clear();

        // Add headers
        RevisionDatesRow.Items.Add("Issue Date");
        RevisionPurposeRow.Items.Add("Purpose");
        RevisionMethodRow.Items.Add("Method");
        RevisionIssuedByRow.Items.Add("Issued By");

        if (DocumentGrid.SelectedItem is not DocumentMetadata selectedDoc)
        {
            return;
        }

        var revisions = selectedDoc.RevisionHistory
            .OrderBy(x => x.Key)
            .ToList();

        foreach (var revision in revisions)
        {
            RevisionDatesRow.Items.Add(revision.Key.ToString("dd/MM/yyyy"));
            RevisionPurposeRow.Items.Add(revision.Value.Purpose);
            RevisionMethodRow.Items.Add(revision.Value.Method);
            RevisionIssuedByRow.Items.Add(revision.Value.IssuedBy);
        }
    }

    private void ImportDocuments_Click(object sender, RoutedEventArgs e)
    {
        var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the project folder containing PDFs to scan",
            UseDescriptionForTitle = true
        };

        if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                _project.ImportDocuments(folderBrowserDialog.SelectedPath);
                UpdateRevisionHistory();
                MessageBox.Show($"Successfully imported {_project.Documents.Count} documents.", 
                    "Import Complete", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing documents: {ex.Message}", 
                    "Import Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
    }

    private void RefreshView_Click(object sender, RoutedEventArgs e)
    {
        // Refresh the document grid view
        var view = CollectionViewSource.GetDefaultView(DocumentGrid.ItemsSource);
        view.Refresh();

        // Reapply any active filters
        FilterDocuments();

        // Update revision history for selected document
        UpdateRevisionHistory();

        // Force grid to update
        DocumentGrid.Items.Refresh();
    }

    private void DocumentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentGrid.SelectedItem is DocumentMetadata selectedDoc)
        {
            UpdateRevisionHistory();

            try
            {
                if (File.Exists(selectedDoc.FilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedDoc.FilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("File not found: " + selectedDoc.FilePath, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Dispose();
    }
}