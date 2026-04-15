using System.IO;

namespace DrawingRegister.App.Helpers;

internal static class PdfReportFilePathResolver
{
    internal static string GetWritablePath(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            throw new ArgumentException("A file path is required.", nameof(requestedPath));
        }

        if (!IsLocked(requestedPath))
        {
            return requestedPath;
        }

        var directory = Path.GetDirectoryName(requestedPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestedPath);
        var extension = Path.GetExtension(requestedPath);

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({index}){extension}");
            if (!File.Exists(candidatePath) || !IsLocked(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException($"Could not find a writable path for '{requestedPath}'.");
    }

    private static bool IsLocked(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
