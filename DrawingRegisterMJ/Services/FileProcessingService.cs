using System;
using System.IO;
using System.Text.RegularExpressions;
using DrawingRegisterMJ.Models;

namespace DrawingRegisterMJ.Services
{
    public class FileProcessingService
    {
        private readonly DatabaseService _databaseService;

        public FileProcessingService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void ProcessDirectory(string rootPath)
        {
            var pdfFiles = Directory.GetFiles(rootPath, "*.pdf", SearchOption.AllDirectories);
            foreach (var filePath in pdfFiles)
            {
                var drawing = ParseDrawingFile(filePath);
                if (drawing != null)
                {
                    _databaseService.InsertOrUpdateDrawing(drawing);
                }
            }
        }

        private Drawing ParseDrawingFile(string filePath)
        {
            var filename = Path.GetFileNameWithoutExtension(filePath);
            var lastModified = File.GetLastWriteTime(filePath);
            var parentFolderName = Directory.GetParent(filePath).Name;
            var projectFolder = GetProjectFolder(filePath);

            DateTime? dateOfIssue = null;
            if (DateTime.TryParseExact(parentFolderName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                dateOfIssue = parsedDate;
            }

            // Regular Expression to match the naming convention
            var regex = new Regex(@"^(?<Project>\d+)-(?<Originator>[A-Z]+\+[A-Z]+)-(?<Volume>\d+)-(?<Level>[A-Z0-9]+)-(?<FileType>[A-Z]+)-(?<Discipline>[A-Z]+)-(?<Number>\d+-\d+)(?:-(?<Revision>[A-Z0-9]+))?(?:-(?<Description>.*))?$");
            
            var match = regex.Match(filename);
            if (match.Success)
            {
                return new Drawing
                {
                    DocumentNumber = filename,
                    DocumentType = "DR", // Default to DR, can be updated based on actual type
                    Package = match.Groups["Volume"].Value,
                    Description = match.Groups["Description"].Success ? match.Groups["Description"].Value.Trim() : string.Empty,
                    Size = "A1", // Default size, can be determined from actual file if needed
                    Revision = match.Groups["Revision"].Success ? match.Groups["Revision"].Value : string.Empty,
                    Project = match.Groups["Project"].Value,
                    Originator = match.Groups["Originator"].Value,
                    Volume = match.Groups["Volume"].Value,
                    Level = match.Groups["Level"].Value,
                    FileType = match.Groups["FileType"].Value,
                    Discipline = match.Groups["Discipline"].Value,
                    Number = match.Groups["Number"].Value,
                    FilePath = filePath,
                    LastModified = lastModified,
                    DateOfIssue = dateOfIssue,
                    ProjectFolder = projectFolder
                };
            }

            return null;
        }

        private string GetProjectFolder(string filePath)
        {
            try
            {
                var directory = new DirectoryInfo(filePath);
                // Navigate up 5 levels to get to the project folder
                for (int i = 0; i < 5 && directory.Parent != null; i++)
                {
                    directory = directory.Parent;
                }
                return directory?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
} 