using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace DrawingRegister.App.Services;

public sealed record CrmProject(string Code, string Title);

/// <summary>
/// Office-wide CMap project list, published as a flat JSON map by the timesheet
/// tooling. Keys carry a trailing dash ("124832-"), values are the titles.
/// </summary>
public static class ProjectCatalog
{
    public const string SharedCataloguePath = @"\\srmjfp01\data\05-LIBRARY\SOFTWARE\MJ SCRIPTS\Timesheet\Projects\project_aliases.json";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private static readonly object Gate = new();
    private static IReadOnlyList<CrmProject> _cached = Array.Empty<CrmProject>();
    private static DateTime _loadedStamp;
    private static DateTime _lastCheckUtc = DateTime.MinValue;
    private static bool _refreshing;

    public static IReadOnlyList<CrmProject> Parse(string json)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string?>>(json.TrimStart('\uFEFF'));
        if (map is null)
            return Array.Empty<CrmProject>();

        var projects = new List<CrmProject>(map.Count);
        foreach (var (key, title) in map)
        {
            var code = key.Trim();
            if (code.EndsWith('-'))
                code = code[..^1];

            if (code.Length == 0 || string.IsNullOrWhiteSpace(title))
                continue;

            projects.Add(new CrmProject(code, title));
        }

        return projects;
    }

    public static IReadOnlyList<CrmProject> Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load CMap project catalogue from {Path}", path);
            return Array.Empty<CrmProject>();
        }
    }

    public static IReadOnlyList<CrmProject> Search(IEnumerable<CrmProject> projects, string query, int limit = 30)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return Array.Empty<CrmProject>();

        return projects
            .Select(p => (Project: p, Key: MatchKey(p, tokens)))
            .Where(x => x.Key.HasValue)
            .OrderBy(x => x.Key!.Value)
            // Code is not part of the key tuple because ValueTuple compares strings
            // culture-sensitively and this ordering has to be ordinal.
            .ThenBy(x => x.Project.Code, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => x.Project)
            .ToList();
    }

    public static string? TitleFor(IEnumerable<CrmProject> projects, string code) =>
        projects.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Title;

    // Deepest path segment that looks like a job number, so the bucket folder
    // (124600) loses to the project folder (124615) beneath it.
    public static string? CodeFromPath(string path) =>
        path.Split('\\', '/')
            .LastOrDefault(segment => ProjectCodePattern.IsMatch(segment));

    private static readonly System.Text.RegularExpressions.Regex ProjectCodePattern =
        new(@"^\d{5,6}[A-Za-z]?$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Cached view of the shared catalogue. Returns immediately with whatever is
    /// cached and refreshes in the background at most once every five minutes.
    /// </summary>
    public static IReadOnlyList<CrmProject> Projects()
    {
        IReadOnlyList<CrmProject> snapshot;
        lock (Gate)
        {
            snapshot = _cached;
            if (_refreshing || DateTime.UtcNow - _lastCheckUtc < CheckInterval)
                return snapshot;

            _refreshing = true;
            _lastCheckUtc = DateTime.UtcNow;
        }

        // Every touch of the share happens off the caller's thread: a disconnected
        // or asleep file server blocks even GetLastWriteTimeUtc for seconds, and
        // the caller here is the UI thread on every keystroke.
        _ = Task.Run(Refresh);
        return snapshot;
    }

    private static void Refresh()
    {
        try
        {
            var stamp = File.GetLastWriteTimeUtc(SharedCataloguePath);
            lock (Gate)
            {
                if (stamp == _loadedStamp)
                    return;
            }

            var loaded = Load(SharedCataloguePath);
            lock (Gate)
            {
                _cached = loaded;
                _loadedStamp = stamp;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to refresh CMap project catalogue from {Path}", SharedCataloguePath);
        }
        finally
        {
            lock (Gate)
            {
                _refreshing = false;
            }
        }
    }

    // Null when some token matches neither field. Rank 0 when every token is in
    // the code, and the numeric part is negated so newer job numbers sort first.
    private static (int Rank, long NegativeNumber)? MatchKey(CrmProject project, string[] tokens)
    {
        var allInCode = true;
        foreach (var token in tokens)
        {
            var inCode = project.Code.Contains(token, StringComparison.OrdinalIgnoreCase);
            if (!inCode && !project.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
                return null;

            allInCode &= inCode;
        }

        return (allInCode ? 0 : 1, -LeadingDigits(project.Code));
    }

    private static long LeadingDigits(string code)
    {
        var end = 0;
        while (end < code.Length && char.IsAsciiDigit(code[end]))
            end++;

        return long.TryParse(code[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
