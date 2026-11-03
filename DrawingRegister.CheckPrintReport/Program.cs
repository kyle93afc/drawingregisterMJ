using DrawingRegister.App.Services;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: DrawingRegister.CheckPrintReport <project-folder> <checking-folder> <output.csv>");
    return 2;
}

try
{
    var rows = CheckStatusReport.Run(args[0], args[1]);
    CheckStatusReport.WriteCsv(rows, args[2]);
    Console.Out.WriteLine($"Exported {rows.Count} row(s) to {args[2]}.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Check-print report failed: {ex.Message}");
    return 1;
}
