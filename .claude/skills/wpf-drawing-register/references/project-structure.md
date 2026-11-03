# Drawing Register Project Structure

## Models

### DocumentMetadata.cs
Core document model with:
- `DocumentNumber`, `Title`, `Size`, `Package`
- `Revisions` list (RevisionInfo objects)
- `Stakeholders` list (StakeholderInfo objects)
- `GenerateRevisionCode()` - creates revision codes (A, B, C... or C01, T01, P01, I01)
- `ToggleDistribution()`, `ToggleCompanyDistribution()`

### RevisionInfo
- `RevisionCode`, `Date`, `PurposeOfIssue`, `MethodOfIssue`, `IssuedBy`
- `FilePath` - revision-specific file location

### ProjectStorage.cs
JSON persistence:
- `SaveProject(path, project)` - serialize to JSON
- `LoadProject(path)` - deserialize from JSON
- Uses System.Text.Json

### ProjectManager.cs
Project operations and state management.

## Dialogs

| Dialog | Purpose |
|--------|---------|
| DocumentEditDialog | Add/edit document metadata |
| RevisionEditDialog | Manage revisions |
| DistributionDialog | Configure distribution |
| CompanyDialog | Manage companies |
| StakeholderDialog | Manage stakeholders |
| BatchEditDialog | Bulk operations |
| DistributionInfoDialog | View summaries |

## Converters

Used in XAML bindings:
- `BoolToVisibilityConverter` - bool to Visibility
- `DateToColumnConverter` - DateTime formatting
- `LatestRevisionConverter` - gets latest revision
- `RevisionColorConverter` - revision status colors

## XAML Patterns

```xml
<!-- DataGrid binding -->
<DataGrid ItemsSource="{Binding Documents}" SelectedItem="{Binding SelectedDocument}">

<!-- Button commands -->
<Button Content="Save" Command="{Binding SaveCommand}"/>

<!-- Visibility binding -->
<StackPanel Visibility="{Binding IsVisible, Converter={StaticResource BoolToVis}}"/>
```

## Code-Behind Patterns

```csharp
// Dialog result
private void OkButton_Click(object sender, RoutedEventArgs e)
{
    DialogResult = true;
    Close();
}

// Data binding refresh
private void RefreshData()
{
    documentsDataGrid.Items.Refresh();
}
```
