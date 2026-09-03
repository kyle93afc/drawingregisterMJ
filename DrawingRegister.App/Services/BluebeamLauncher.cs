using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DrawingRegister.App.Services;

public static class BluebeamLauncher
{
    // Opens every path as a tab in one Revu window. Falls back to the shell's PDF handler per file; returns false when it did.
    public static bool Open(params string[] paths)
    {
        var revu = FindRevu();
        if (revu is null)
        {
            foreach (var path in paths)
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return false;
        }

        var startInfo = new ProcessStartInfo { FileName = revu, UseShellExecute = false };
        foreach (var path in paths)
            startInfo.ArgumentList.Add(path);
        Process.Start(startInfo);
        return true;
    }

    // ponytail: Revu.exe from the .pdf file association, else the newest install under Program Files.
    private static string? FindRevu()
    {
        var progId = Registry.ClassesRoot.OpenSubKey(".pdf")?.GetValue(null) as string;
        var command = string.IsNullOrEmpty(progId) ? null
            : Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command")?.GetValue(null) as string;
        if (command is not null && Regex.Match(command, @"""?([^""]*Revu\.exe)", RegexOptions.IgnoreCase) is { Success: true } m && File.Exists(m.Groups[1].Value))
            return m.Groups[1].Value;

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Bluebeam Software", "Bluebeam Revu");
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "Revu.exe", SearchOption.AllDirectories).OrderByDescending(p => p).FirstOrDefault()
            : null;
    }
}
