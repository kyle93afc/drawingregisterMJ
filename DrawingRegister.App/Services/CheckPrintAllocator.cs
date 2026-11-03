using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public static class CheckPrintAllocator
{
    private const string ReservationFileName = "check_print_reservations.json";
    private const string LockFileName = ".check_print_reservations.lock";

    public static CheckPrintReservation ReserveNext(string projectFolder, string documentCode, string revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var storagePath = Path.Combine(projectFolder, "project_data.json");
        if (!Directory.Exists(projectFolder) || !File.Exists(storagePath))
            throw new DirectoryNotFoundException("The shared project store is unavailable.");

        using var reservationLock = AcquireLock(Path.Combine(projectFolder, LockFileName));
        using var storageStream = File.Open(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var storage = JsonSerializer.Deserialize<ProjectStorage>(storageStream)
            ?? throw new InvalidDataException("The shared project store is invalid.");
        var reservationsPath = Path.Combine(projectFolder, ReservationFileName);
        var reservations = Load(reservationsPath);
        documentCode = documentCode.Trim();
        revision = revision.Trim();

        // CP numbers run per drawing across revisions (P01-CP01, T01-CP02, T01-CP03), so scope by document code only.
        var highestCp = (storage.CheckPrints ?? [])
            .Where(check => IsSameDrawing(check.DocumentCode, documentCode))
            .Select(check => check.Cp)
            .Concat(reservations
                .Where(reservation => IsSameDrawing(reservation.DocumentCode, documentCode))
                .Select(reservation => reservation.Cp))
            .DefaultIfEmpty()
            .Max();

        var reservation = new CheckPrintReservation(documentCode, revision, checked(highestCp + 1));
        reservations.Add(reservation);
        Save(reservationsPath, reservations);
        return reservation;
    }

    private static bool IsSameDrawing(string candidateCode, string documentCode) =>
        string.Equals(candidateCode, documentCode, StringComparison.OrdinalIgnoreCase);

    private static List<CheckPrintReservation> Load(string path)
    {
        if (!File.Exists(path))
            return [];

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<CheckPrintReservation>>(stream)
            ?? throw new InvalidDataException("The check-print reservation store is invalid.");
    }

    private static void Save(string path, List<CheckPrintReservation> reservations)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, reservations);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static FileStream AcquireLock(string path)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(25);
            }
        }

        throw new IOException("The shared check-print reservation store is busy. Please try again.");
    }
}

public sealed record CheckPrintReservation(string DocumentCode, string Revision, int Cp);
