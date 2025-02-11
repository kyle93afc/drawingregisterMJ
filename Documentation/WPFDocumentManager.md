Markdown

# Building a Document/Drawing Management Tool with .NET (C#) and WPF

Excellent choice! .NET with WPF is a robust and well-suited platform for building a feature-rich and performant Windows desktop application like your document/drawing management tool.

Let's dive deeper into how to implement Option 1 (.NET/WPF) and break it down into actionable steps.

## Detailed Implementation Plan - .NET (C#) and WPF

Here's a more detailed plan, covering key aspects from project setup to functionality implementation:

### 1. Project Setup in Visual Studio

*   **Install Visual Studio:** If you don't have it already, download and install Visual Studio Community (it's free for individual developers and small teams). Make sure to include the ".NET desktop development" workload during installation.
*   **Create a New WPF Project:**
    1.  Open Visual Studio.
    2.  Click "Create a new project."
    3.  Search for and select "WPF Application" (make sure it's for C# and not .NET Framework or .NET - the latest ".NET" is recommended, but ".NET Framework" will also work).
    4.  Click "Next," configure project name (e.g., "DrawingManager"), location, and solution name.
    5.  Click "Create."
    6.  Visual Studio will create a basic WPF project with a `MainWindow.xaml` (for UI definition) and `MainWindow.xaml.cs` (for code-behind logic).

### 2. Data Modeling (C# Classes - DocumentMetadata)

Let's define a C# class to represent the metadata for each drawing. Based on your Excel register and file structure, consider properties like:

```csharp
using System;

namespace DrawingManager.Models // Consider creating a "Models" folder in your project
{
    public class DocumentMetadata
    {
        public int Id { get; set; } // Primary key for database - let EF Core manage this
        public string DocumentNumber { get; set; }
        public string DocumentType { get; set; } // DR, SK, etc.
        public string Package { get; set; }
        public string Description { get; set; }
        public string Size { get; set; } // A1, A2, A, B, etc.
        public DateTime? IssueDate { get; set; } // Use nullable DateTime? if issue date might be missing
        public string PurposeOfIssue { get; set; }
        public string MethodOfIssue { get; set; }
        public string IssuedBy { get; set; }
        public string Revision { get; set; } // A, B, C, D, etc.
        public string FilePath { get; set; } // Full path to the PDF file

        // Optional: Add more properties based on your needs,
        // like Project Number, Client Number, Discipline, etc.
    }
}
Id: This is crucial for the database primary key and will be managed by Entity Framework Core.
Properties: Map to columns in your Excel register and relevant parts of your filenames. Use appropriate data types (string, DateTime?, int). Make properties nullable (?) if the data might be missing in some cases.
Namespace: Organize your classes in namespaces (e.g., DrawingManager.Models) for better code structure.
3. Data Storage - SQLite and Entity Framework Core (EF Core)
Install EF Core and SQLite Provider:
Open NuGet Package Manager in Visual Studio (Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution...).
In the "Browse" tab, search for and install these packages:
Microsoft.EntityFrameworkCore.Sqlite
Microsoft.EntityFrameworkCore.Tools (for migrations)
Create Database Context (DocumentContext):
Create a new folder in your project, e.g., "Data".
Create a new C# class within the "Data" folder, e.g., DocumentContext.cs.
Make it inherit from DbContext:
C#

using Microsoft.EntityFrameworkCore;
using DrawingManager.Models; // Assuming your DocumentMetadata class is in Models namespace

namespace DrawingManager.Data // Consider a "Data" namespace
{
    public class DocumentContext : DbContext
    {
        public DbSet<DocumentMetadata> Documents { get; set; } // DbSet for your DocumentMetadata entities

        public DocumentContext(DbContextOptions<DocumentContext> options) : base(options)
        { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=DrawingManager.db"); // SQLite database file name
        }
    }
}
*   **DbContext:** EF Core's main class for database interaction.
*   `DbSet<DocumentMetadata>`: Represents the Documents table in your database, holding `DocumentMetadata` entities.
*   `OnConfiguring`: Specifies you are using SQLite and the database file name (`DrawingManager.db` - you can choose a different name).
Create Database Migration:
Open Package Manager Console (Tools -> NuGet Package Manager -> Package Manager Console).
Run the command: Add-Migration InitialCreate
EF Core will create a "Migrations" folder with code to create the database schema based on your DocumentContext and DocumentMetadata class.
Apply Migration to Create Database:
In Package Manager Console, run: Update-Database
EF Core will create the DrawingManager.db SQLite database file in your project's output directory (usually in bin\Debug or bin\Release).
4. User Interface (WPF - MainWindow.xaml and MainWindow.xaml.cs)
Basic UI Structure (Conceptual XAML): Open MainWindow.xaml and start designing your UI. Here's a very basic example to get you started:
XML

<Window x:Class="DrawingManager.MainWindow"
        xmlns="[http://schemas.microsoft.com/winfx/2006/xaml/presentation](http://schemas.microsoft.com/winfx/2006/xaml/presentation)"
        xmlns:x="[http://schemas.microsoft.com/winfx/2006/xaml](http://schemas.microsoft.com/winfx/2006/xaml)"
        Title="Drawing Manager" Height="600" Width="1000">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="200"/> <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBox Grid.Column="1" Margin="5" Height="25" VerticalAlignment="Top"  />
        <TreeView Grid.Row="1" Margin="5" Grid.Column="0" />
        <ListView Grid.Row="1" Margin="5" Grid.Column="1" >
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Document Number" DisplayMemberBinding="{Binding DocumentNumber}" Width="150"/>
                    <GridViewColumn Header="Description"  DisplayMemberBinding="{Binding Description}"  Width="300"/>
                </GridView>
            </ListView.View>
        </ListView>
        <DocumentViewer Grid.Row="1" Margin="5" Grid.Column="1" Visibility="Collapsed" Name="pdfViewer" />
    </Grid>
</Window>
*   **Grid:** Used for layout. Column and Row definitions to divide the window.
*   **TextBox:** For search input.
*   **TreeView:** For the file system browser. You'll need to populate this programmatically in your C# code-behind.
*   **ListView:** To display the list of documents. `GridView` inside `ListView.View` creates tabular columns. `DisplayMemberBinding` connects columns to properties of your `DocumentMetadata` class.
*   **DocumentViewer:** A basic WPF control for displaying documents (including PDFs, though might need a more robust 3rd party control for PDF features). `Visibility="Collapsed"` starts it hidden; you'll make it visible when a PDF is selected.
Data Binding: WPF is designed for data binding. The DisplayMemberBinding in the ListView example demonstrates this. You will bind UI elements to properties of your DocumentMetadata objects and collections of these objects.
5. Functionality Implementation (C# Code-Behind - MainWindow.xaml.cs)
File System Browsing (TreeView):
In MainWindow.xaml.cs, in the MainWindow constructor or Loaded event handler, write code to populate the TreeView. Use System.IO.DirectoryInfo and System.IO.FileInfo to get directories and files. Create TreeViewItem objects for folders and files and add them to the TreeView. You'll likely start at a root directory (e.g.,