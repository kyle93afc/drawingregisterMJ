using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrawingRegister.App.Models;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace DrawingRegister.App;

public partial class OrganizationSettingsDialog : Window
{
    public bool SettingsChanged { get; private set; }

    public OrganizationSettingsDialog()
    {
        InitializeComponent();

        var settings = AppSettings.Current;
        var orgId = settings.DefaultOrganizationId;

        foreach (ComboBoxItem item in DefaultOrgCombo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), orgId, StringComparison.OrdinalIgnoreCase))
            {
                DefaultOrgCombo.SelectedItem = item;
                break;
            }
        }

        if (DefaultOrgCombo.SelectedItem == null && DefaultOrgCombo.Items.Count > 0)
        {
            DefaultOrgCombo.SelectedIndex = 0;
        }

        ServerFolderPathBox.Text = settings.DefaultProjectsFolder ?? string.Empty;
    }

    private void BrowseServerFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select default projects or server folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(ServerFolderPathBox.Text) ? ServerFolderPathBox.Text : string.Empty
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ServerFolderPathBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppSettings.Current;

        if (DefaultOrgCombo.SelectedItem is ComboBoxItem selected)
        {
            settings.DefaultOrganizationId = selected.Tag?.ToString() ?? OrganizationRegistry.Default.Id;
        }

        settings.DefaultProjectsFolder = ServerFolderPathBox.Text.Trim();
        settings.Save();

        SettingsChanged = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Header_DragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
