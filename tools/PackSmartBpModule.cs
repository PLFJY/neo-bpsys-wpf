#:package SharpCompress@0.49.1

using SharpCompress.Common;
using SharpCompress.Writers;

if (args.Length != 2)
{
    Fail("Usage: dotnet run tools/PackSmartBpModule.cs -- <input-directory> <output-7z>");
}

var inputDirectory = Path.GetFullPath(args[0]);
var outputArchive = Path.GetFullPath(args[1]);

if (!Directory.Exists(inputDirectory))
{
    Fail($"Input directory does not exist: {inputDirectory}");
}

var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (files.Length == 0)
{
    Fail($"Input directory is empty: {inputDirectory}");
}

var outputDirectory = Path.GetDirectoryName(outputArchive);
if (string.IsNullOrWhiteSpace(outputDirectory))
{
    Fail($"Output archive path is invalid: {outputArchive}");
}

var totalBytes = files.Sum(file => new FileInfo(file).Length);
Console.WriteLine($"Packing SmartBP module archive");
Console.WriteLine($"Input:  {inputDirectory}");
Console.WriteLine($"Output: {outputArchive}");
Console.WriteLine($"Files:  {files.Length:N0}");
Console.WriteLine($"Size:   {FormatBytes(totalBytes)}");

Directory.CreateDirectory(outputDirectory!);
if (File.Exists(outputArchive))
{
    File.Delete(outputArchive);
}

try
{
    await using var output = File.Create(outputArchive);
    using var writer = WriterFactory.OpenWriter(
        output,
        ArchiveType.SevenZip,
        new WriterOptions(CompressionType.LZMA));

    for (var index = 0; index < files.Length; index++)
    {
        var file = files[index];
        var relativePath = Path.GetRelativePath(inputDirectory, file).Replace('\\', '/');
        var percent = (index + 1) * 100.0 / files.Length;
        Console.WriteLine($"[{index + 1:N0}/{files.Length:N0}] {percent,6:0.00}% {relativePath}");
        writer.Write(relativePath, file);
    }
}
catch (Exception ex)
{
    Fail($"Failed to create SmartBP module archive '{outputArchive}': {ex.Message}");
}

if (!File.Exists(outputArchive) || new FileInfo(outputArchive).Length == 0)
{
    Fail($"SmartBP module archive was not created or is empty: {outputArchive}");
}

Console.WriteLine($"Created SmartBP module archive: {outputArchive}");
Console.WriteLine($"Archive size: {FormatBytes(new FileInfo(outputArchive).Length)}");

static void Fail(string message)
{
    Console.Error.WriteLine(message);
    Environment.Exit(1);
}

static string FormatBytes(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB"];
    var value = (double)bytes;
    var unitIndex = 0;
    while (value >= 1024 && unitIndex < units.Length - 1)
    {
        value /= 1024;
        unitIndex++;
    }

    return $"{value:0.##} {units[unitIndex]}";
}
