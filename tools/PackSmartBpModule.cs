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

    foreach (var file in files)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file).Replace('\\', '/');
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

static void Fail(string message)
{
    Console.Error.WriteLine(message);
    Environment.Exit(1);
}
