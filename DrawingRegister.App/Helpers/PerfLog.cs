using System;
using System.Diagnostics;
using Serilog;

namespace DrawingRegister.App.Helpers;

// Lightweight [PERF] logging. Writes a single "done in Xms" line on Dispose so
// you can grep the Serilog file for "[PERF]" and rank hot paths.
public static class PerfLog
{
    public static Scope Begin(string name) => new Scope(name);

    public static void Event(string name, long elapsedMs, int? count = null)
    {
        if (count.HasValue)
            Log.Information("[PERF] {Name} count={Count} took={ElapsedMs}ms", name, count.Value, elapsedMs);
        else
            Log.Information("[PERF] {Name} took={ElapsedMs}ms", name, elapsedMs);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly string _name;
        private readonly long _startTicks;

        public Scope(string name)
        {
            _name = name;
            _startTicks = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsedMs = (Stopwatch.GetTimestamp() - _startTicks) * 1000 / Stopwatch.Frequency;
            Log.Information("[PERF] {Name} took={ElapsedMs}ms", _name, elapsedMs);
        }
    }
}
