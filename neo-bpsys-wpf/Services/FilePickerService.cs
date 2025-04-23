using Microsoft.Win32;

namespace neo_bpsys_wpf.Services
{
    public class FilePickerService : IFilePickerService
    {
        public string? PickImage()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter =
                    "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp",
            };

            if (openFileDialog.ShowDialog() != true)
                return null;

            return openFileDialog.FileName;
        }

        public string? PickJsonFile()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Json文件 (*.json) | *.json",
                DefaultDirectory = Environment.CurrentDirectory,
            };

            if (openFileDialog.ShowDialog() != true)
                return null;

            return openFileDialog.FileName;
        }
    }
}
