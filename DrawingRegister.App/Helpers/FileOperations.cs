using System;
using System.IO;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Helpers
{
    public static class FileOperations
    {
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
    }
} 