namespace neo_bpsys_wpf.Abstractions.Services
{
    public interface IFilePickerService
    {
        public string? PickImage();
        public string? PickJsonFile();
    }
}
