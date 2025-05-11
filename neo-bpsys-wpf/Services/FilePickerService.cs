using Microsoft.Win32;
using System.IO;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 文件选择服务实现类，提供图像文件和JSON配置文件的选择功能
    /// </summary>
    public class FilePickerService : IFilePickerService
    {
        /// <summary>
        /// 打开图片文件选择对话框
        /// </summary>
        /// <returns>用户选择的图片文件路径，若取消操作则返回null</returns>
        /// <remarks>
        /// 支持的图片格式包括：PNG、JPG、JPEG、BMP、GIF、ICO、TIF、TIFF、SVG、WEBP
        /// </remarks>
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
        /// 打开JSON文件选择对话框
        /// </summary>
        /// <returns>用户选择的JSON文件路径，若取消操作则返回null</returns>
        /// <remarks>
        /// 文件对话框默认显示路径为程序集所在目录下的Resources文件夹
        /// </remarks>
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