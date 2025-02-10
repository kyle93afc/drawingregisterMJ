# Drawing Register Application

A WPF application for managing and tracking architectural drawings and documents.

## Features

- Track and manage architectural drawings and documents
- Automatic file scanning and parsing
- SQLite database for persistent storage
- Modern Material Design UI
- Support for drawing revisions and distribution tracking

## Requirements

- .NET 7.0 or later
- Windows OS

## Installation

1. Clone the repository
2. Open the solution in Visual Studio
3. Build and run the application

## Usage

1. Launch the application
2. Click "Select Project Folder" to choose the root directory containing your PDF drawings
3. The application will automatically scan and parse drawing files following the naming convention:
   `[ProjectNumber]-[Originator]-[Volume]-[Level]-[FileType]-[Discipline]-[Number]-[Revision]-[Description]`
4. The drawings will be displayed in the grid below
5. Use the "Refresh" button to update the drawing list

## File Naming Convention

The application expects drawing files to follow this naming convention:
- Project Number: Numeric identifier
- Originator: Company/Organization code (e.g., "M+J")
- Volume: Numeric volume identifier
- Level: Level code
- File Type: Document type code
- Discipline: Discipline code
- Number: Drawing number
- Revision: Optional revision identifier
- Description: Optional description

Example: `240378-M+J-00-XX-RE-A-00-01-A-GENERAL ARRANGEMENT`

## Project Structure

The drawings should be organized in folders by date (YYYYMMDD format) for proper date tracking.

Example folder structure:
```
Project Root/
  ├── 20240201/
  │   ├── drawing1.pdf
  │   └── drawing2.pdf
  └── 20240205/
      ├── drawing3.pdf
      └── drawing4.pdf
``` 