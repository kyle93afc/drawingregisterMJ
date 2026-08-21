# Drawing Register

A WPF application for managing and tracking engineering drawings and documents with revision history.

## Features

- Document management with revision tracking
- Advanced filtering and search capabilities
- Date-based filtering for document revisions
- PDF report generation
- Modern WPF interface with data grid visualization
- Project metadata management (Discipline, Reg No, Project No, etc.)

## Requirements

- Windows OS
- .NET 8.0
- Visual Studio 2022 or later

## Dependencies

- PDFsharp-MigraDoc-GDI (v1.50.5147)
- System.Text.Encoding.CodePages (v8.0.0)

## Setup

1. Clone the repository
2. Open the solution in Visual Studio
3. Restore NuGet packages
4. Build and run the application

## Usage

1. Launch the application
2. Enter project metadata (Discipline, Reg No, Project No, etc.)
3. Add/Edit documents in the data grid
4. Use search and date filters to find specific documents
5. Generate PDF reports as needed

## Building

```powershell
dotnet build
dotnet run --project DrawingRegister.App
```

## Scheduled check-print status report

Run the headless report with the project folder, checking folder, and output CSV path:

```powershell
DrawingRegister.CheckPrintReport.exe "C:\Projects\17749" "C:\Projects\17749\Checking" "C:\Reports\17749-checks.csv"
```

From a source checkout, use:

```powershell
dotnet run --project DrawingRegister.CheckPrintReport -- "C:\Projects\17749" "C:\Projects\17749\Checking" "C:\Reports\17749-checks.csv"
```

For Windows Task Scheduler, set **Program/script** to the published `DrawingRegister.CheckPrintReport.exe` and **Add arguments** to the three quoted paths above. Exit code `0` means the CSV was written, `1` means the scan or export failed, and `2` means the arguments were invalid. The runner reads `project_data.json` and check-print PDFs without starting WPF or changing either input.

## License

[Your License Here] 