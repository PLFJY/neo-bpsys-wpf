namespace neo_bpsys.Core.Abstractions.Services;

public interface IFilePickerService
{
    string? PickImage();
    string? PickJsonFile();
}
