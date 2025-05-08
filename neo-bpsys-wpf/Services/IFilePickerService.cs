namespace neo_bpsys_wpf.Services
{
    public interface IFilePickerService
    {
        public string? PickImage();
        public string? PickJsonFile();
    }
}
