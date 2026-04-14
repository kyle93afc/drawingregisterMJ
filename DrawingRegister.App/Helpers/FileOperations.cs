using System;
using System.IO;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Helpers
{
    public sealed class LatestPdfExportResult
    {
        public string ExportFolderPath { get; init; } = string.Empty;
        public int CopiedCount { get; set; }
        public int SkippedCount => SkippedFiles.Count;
        public List<PdfExportSkippedFileInfo> SkippedFiles { get; } = new();
    }

    public sealed class PdfExportSkippedFileInfo
    {
        public string DocumentNumber { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }

    public static class FileOperations
    {
        public static LatestPdfExportResult CollectLatestPdfs(IEnumerable<DocumentMetadata> documents, string destinationParentPath, DateTime exportTimestamp)
        {
            ArgumentNullException.ThrowIfNull(documents);

            if (string.IsNullOrWhiteSpace(destinationParentPath))
            {
                throw new ArgumentException("Destination folder is required.", nameof(destinationParentPath));
            }

            Directory.CreateDirectory(destinationParentPath);

            var exportFolderPath = GetUniqueExportFolderPath(destinationParentPath, exportTimestamp);
            Directory.CreateDirectory(exportFolderPath);

            var result = new LatestPdfExportResult
            {
                ExportFolderPath = exportFolderPath
            };

            foreach (var document in documents)
            {
                if (document == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(document.FilePath))
                {
                    result.SkippedFiles.Add(new PdfExportSkippedFileInfo
                    {
                        DocumentNumber = document.DocumentNumber,
                        FilePath = string.Empty,
                        Reason = "No PDF file path is recorded."
                    });
                    continue;
                }

                if (!File.Exists(document.FilePath))
                {
                    result.SkippedFiles.Add(new PdfExportSkippedFileInfo
                    {
                        DocumentNumber = document.DocumentNumber,
                        FilePath = document.FilePath,
                        Reason = "Source PDF not found."
                    });
                    continue;
                }

                var destinationFilePath = GetUniqueExportFilePath(exportFolderPath, Path.GetFileName(document.FilePath), document.DocumentNumber);
                File.Copy(document.FilePath, destinationFilePath, overwrite: false);
                result.CopiedCount++;
            }

            return result;
        }

        public static bool RenameDocumentFile(DocumentMetadata document, string newDocumentNumber, string newDescription)
        {
            try
            {
                if (string.IsNullOrEmpty(document.FilePath) || !File.Exists(document.FilePath))
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(document.FilePath) ?? string.Empty;
                string extension = Path.GetExtension(document.FilePath);
                string newFileName = $"{newDocumentNumber}_{newDescription.Replace(" ", "_")}{extension}";
                string newFilePath = Path.Combine(directory, newFileName);

                // Don't rename if the file already exists with that name
                if (File.Exists(newFilePath) && !string.Equals(document.FilePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Don't rename if the paths are the same
                if (string.Equals(document.FilePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                File.Move(document.FilePath, newFilePath);
                document.FilePath = newFilePath;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetUniqueExportFolderPath(string destinationParentPath, DateTime exportTimestamp)
        {
            var baseFolderPath = Path.Combine(destinationParentPath, $"Latest Drawings {exportTimestamp:yyyy-MM-dd HH-mm}");

            if (!Directory.Exists(baseFolderPath))
            {
                return baseFolderPath;
            }

            var suffix = 2;
            while (true)
            {
                var candidatePath = $"{baseFolderPath} ({suffix})";
                if (!Directory.Exists(candidatePath))
                {
                    return candidatePath;
                }

                suffix++;
            }
        }

        private static string GetUniqueExportFilePath(string exportFolderPath, string fileName, string documentNumber)
        {
            var candidatePath = Path.Combine(exportFolderPath, fileName);
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var safeDocumentNumber = SanitizeFileNamePart(documentNumber);

            if (!string.IsNullOrWhiteSpace(safeDocumentNumber))
            {
                candidatePath = Path.Combine(exportFolderPath, $"{nameWithoutExtension}_{safeDocumentNumber}{extension}");
                if (!File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            var suffix = 2;
            var baseName = !string.IsNullOrWhiteSpace(safeDocumentNumber)
                ? $"{nameWithoutExtension}_{safeDocumentNumber}"
                : nameWithoutExtension;

            while (true)
            {
                candidatePath = Path.Combine(exportFolderPath, $"{baseName}_{suffix}{extension}");
                if (!File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                suffix++;
            }
        }

        private static string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = value;
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            return sanitized.Trim();
        }
    }
} 
