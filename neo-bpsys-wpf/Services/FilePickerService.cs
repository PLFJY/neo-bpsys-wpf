using Microsoft.Win32;
using System.IO;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 文件选择服务，实现了 <see cref="IFilePickerService"/> 接口
    /// 用于封装文件选择操作
    /// </summary>
    public class FilePickerService : IFilePickerService
    {
        /// <summary>
        /// 选择图片
        /// </summary>
        /// <returns>返回图片文件路径</returns>
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

        /// <summary>
        /// 选择Json文件
        /// </summary>
        /// <returns>返回Json文件路径</returns>
        public string? PickJsonFile()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Json文件 (*.json) | *.json",
                DefaultDirectory = Path.Combine(Environment.CurrentDirectory, "Resources"),
            };

            if (openFileDialog.ShowDialog() != true)
                return null;

            return openFileDialog.FileName;
        }
    }
}
