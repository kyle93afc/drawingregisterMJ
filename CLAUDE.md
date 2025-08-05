# Drawing Register Application - Codebase Overview

## Project Type
WPF desktop application for managing engineering drawings and documents with comprehensive revision tracking and distribution management.

## Technology Stack
- **Framework**: .NET 8.0 with WPF (Windows Presentation Foundation)
- **UI**: XAML-based interface with Material Design styling
- **PDF Generation**: QuestPDF (v2023.12.1)
- **Architecture**: MVVM pattern with data binding
- **Storage**: JSON-based file storage
- **Platform**: Windows desktop application

## Core Features

### 1. Document Management
- Track engineering drawings and documents with extensive metadata
- Support for multiple document types and packages
- File path tracking and management
- Document attributes: size, issue date, purpose of issue, method of issue, issued by

### 2. Revision Control System
- Full revision history tracking with date-based versioning
- Automatic revision code generation:
  - Alphabetical sequence (A, B, C...)
  - Purpose-based prefixes:
    - C = Construction (C01, C02...)
    - T = Tender (T01, T02...)
    - P = Planning (P01, P02...)
    - I = Information (I01, I02...)
- Latest revision highlighting and filtering
- Revision-specific file paths
- Each revision tracks: purpose, method, issued by, distribution status

### 3. Distribution Management
- Track document distribution to stakeholders and companies
- Date-based distribution tracking
- Stakeholder management with company associations
- Distribution summary reports
- Toggle distribution status per stakeholder/company per issue date

### 4. Project Organization
- Project metadata:
  - Project Number
  - Project Name
  - Discipline
  - Client Number
  - Register Number
- Multi-project support
- JSON-based project storage

### 5. User Interface Features
- Modern data grid with advanced filtering and search capabilities
- Date-based document filtering
- Batch editing capabilities
- Progress tracking for bulk operations
- Custom dialogs for specialized editing tasks
- Red (#eb1845) and gray color scheme with modern styling

## Directory Structure

```
DrawingRegister.App/
├── Models/                    # Data models and business logic
│   ├── DocumentMetadata.cs   # Core document model with revision tracking
│   ├── DrawingProject.cs     # Project model
│   ├── DistributionCompany.cs
│   ├── DistributionManager.cs
│   ├── ProjectManager.cs
│   └── ProjectStorage.cs     # JSON persistence layer
├── Converters/               # WPF value converters
│   ├── DateToColumnConverter.cs
│   ├── LatestRevisionConverter.cs
│   ├── RevisionColorConverter.cs
│   └── BoolToVisibilityConverter.cs
├── Helpers/                  # Utility classes
│   └── FileOperations.cs
├── Resources/                # Images and assets
│   ├── company-logo.png
│   └── WHITE LOGO RED BACKGROUND.jpg
├── MainWindow.xaml/.cs       # Main application window
├── App.xaml/.cs             # Application entry point
└── [Various Dialog files]    # Specialized editing dialogs
```

## Key Components

### Models
- **DocumentMetadata**: Core document model with revision history, stakeholder tracking, and distribution management
- **RevisionInfo**: Stores revision-specific information including file paths
- **StakeholderInfo**: Tracks stakeholder details and distribution dates
- **DistributionCompany**: Company information for distribution tracking
- **ProjectStorage**: Handles JSON serialization/deserialization of project data

### Dialogs
- **DocumentEditDialog**: Add/edit document metadata
- **RevisionEditDialog**: Manage document revisions
- **DistributionDialog**: Configure document distribution
- **CompanyDialog**: Manage distribution companies
- **StakeholderDialog**: Manage stakeholders
- **BatchEditDialog**: Bulk edit multiple documents
- **DistributionInfoDialog**: View distribution summaries

### Key Methods
- `DocumentMetadata.GenerateRevisionCode()`: Intelligent revision code generation
- `DocumentMetadata.ToggleDistribution()`: Toggle stakeholder distribution
- `DocumentMetadata.ToggleCompanyDistribution()`: Toggle company distribution
- `ProjectStorage.SaveProject()`: Persist project to JSON
- `ProjectStorage.LoadProject()`: Load project from JSON

## Development Guidelines

### Building the Project
```powershell
dotnet build
dotnet run --project DrawingRegister.App
```

### Requirements
- Windows OS
- .NET 8.0 SDK
- Visual Studio 2022 or later (recommended)

### Testing Commands
Since no specific test commands were found in the codebase, consider running:
```powershell
dotnet build
```

## Architecture Notes
- Uses WPF data binding extensively for UI updates
- MVVM pattern implementation with ViewModels for complex dialogs
- Custom value converters for display logic
- JSON file format for data persistence (no database required)
- Event-driven architecture for user interactions

## Typical Workflow
1. Launch application
2. Create or load a project with metadata
3. Add documents with initial revision
4. Track revisions as documents are updated
5. Configure distribution to stakeholders/companies
6. Generate PDF reports as needed
7. Save project to JSON file for persistence