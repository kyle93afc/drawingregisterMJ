# Drawing Register

A WPF application for managing and tracking engineering drawings and documents with revision history. This application helps engineering and architectural firms maintain organized digital records of their technical drawings with comprehensive revision tracking.

## Features

- Document management with revision tracking
- Advanced filtering and search capabilities
  - Date-based filtering for document revisions
  - Search by document number, description, and metadata
  - Filter by discipline, package, and status
- PDF report generation using PDFsharp
- Modern WPF interface with data grid visualization
- Project metadata management
  - Discipline tracking
  - Registration number management
  - Project number organization
  - Client information
  - Package categorization

## System Requirements

- Windows 10 or Windows 11 (64-bit)
- .NET 8.0 Runtime
- 4GB RAM recommended
- At least 200MB of free disk space
- 1366x768 or higher screen resolution

## Installation

### Option 1: Using the Installer

1. Download the latest MSI installer from the releases page
2. Run the installer and follow the setup wizard
3. Launch the application from the Start Menu

### Option 2: Portable Version

1. Extract all contents of the zip file to a folder on your computer
2. Keep all files and folders together in the same directory structure
3. Double-click `DrawingRegister.App.exe` to launch the application

## Development Setup

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- Git (for version control)

### Dependencies

- PDFsharp-MigraDoc-GDI (v1.50.5147)
- System.Text.Encoding.CodePages (v8.0.0)

### Building from Source

1. Clone the repository:
```powershell
git clone [repository-url]
cd DrawingRegister
```

2. Restore dependencies and build:
```powershell
dotnet restore
dotnet build
```

3. Run the application:
```powershell
dotnet run --project DrawingRegister.App
```

## Usage Guide

1. **Initial Setup**
   - Launch the application
   - Enter project metadata (Discipline, Registration Number, Project Number)
   - Configure any project-specific settings

2. **Managing Documents**
   - Add new documents using the data grid interface
   - Edit existing document information
   - Track document revisions
   - Mark documents as superseded when needed

3. **Search and Filter**
   - Use the search bar for quick document lookup
   - Apply date filters to find documents by revision date
   - Filter by discipline, package, or other metadata
   - Sort columns to organize your view

4. **Generating Reports**
   - Select documents to include in the report
   - Choose the report template
   - Generate PDF output
   - Save or print the generated report

## Troubleshooting

If the application doesn't start:

1. Verify that .NET 8.0 Runtime is installed
2. Ensure all files are extracted from the zip archive (if using portable version)
3. Check Windows Event Viewer for error details
4. Try running as administrator: right-click → "Run as administrator"

## Support

For technical support or to report issues:
1. Open an issue on our GitHub repository
2. Contact the development team at [contact information]
3. Check the documentation in the `docs` folder

## License

Copyright © 2024 [Your Organization]
All rights reserved.

[Specify your license terms here] 