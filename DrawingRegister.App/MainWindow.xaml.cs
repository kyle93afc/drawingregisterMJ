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
    private ObservableCollection<List<string>> _revisionRows = new();

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

    public ObservableCollection<List<string>> RevisionRows
    {
        get => _revisionRows;
        set
        {
            _revisionRows = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevisionRows)));
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

        // Bind grids to ProjectManager collections
        DocumentGrid.ItemsSource = _project.Documents;
        RevisionHistoryControl.ItemsSource = RevisionRows;
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
        if (DocumentGrid.SelectedItem is not DocumentMetadata selectedDoc)
        {
            RevisionRows.Clear();
            return;
        }

        var revisions = selectedDoc.RevisionHistory
            .OrderBy(x => x.Key)
            .ToList();

        var rows = new List<List<string>>
        {
            new List<string> { "Issue Date" }.Concat(revisions.Select(r => r.Key.ToString("dd/MM/yyyy"))).ToList(),
            new List<string> { "Purpose" }.Concat(revisions.Select(r => r.Value.Purpose)).ToList(),
            new List<string> { "Method" }.Concat(revisions.Select(r => r.Value.Method)).ToList(),
            new List<string> { "Issued By" }.Concat(revisions.Select(r => r.Value.IssuedBy)).ToList()
        };

        RevisionRows = new ObservableCollection<List<string>>(rows);
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