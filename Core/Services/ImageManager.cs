// مسیر فایل: Core/Services/ImageManager.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TradingJournal.Core.Services
{
    public class ImageManager
    {
        private readonly string _imagesPath;
        private readonly string _thumbnailsPath;
        private readonly int _thumbnailSize = 200;
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public ImageManager()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            _imagesPath = Path.Combine(basePath, "Images");
            _thumbnailsPath = Path.Combine(_imagesPath, "Thumbnails");
            
            Directory.CreateDirectory(_imagesPath);
            Directory.CreateDirectory(_thumbnailsPath);
        }

        public async Task<ImageSaveResult> SaveImageAsync(string sourcePath, int tradeId, string description = null)
        {
            var result = new ImageSaveResult();

            try
            {
                // Validate file
                if (!File.Exists(sourcePath))
                {
                    result.Success = false;
                    result.Message = "فایل تصویر یافت نشد";
                    return result;
                }

                var fileInfo = new FileInfo(sourcePath);
                
                // Check file size
                if (fileInfo.Length > _maxFileSize)
                {
                    result.Success = false;
                    result.Message = $"حجم فایل از {_maxFileSize / 1024 / 1024}MB بیشتر است";
                    return result;
                }

                // Check file type
                var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                if (!Array.Exists(validExtensions, ext => ext.Equals(fileInfo.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Success = false;
                    result.Message = "فرمت فایل پشتیبانی نمی‌شود";
                    return result;
                }

                // Create folder structure: Images/Year/Month/TradeId/
                var year = DateTime.Now.Year.ToString();
                var month = DateTime.Now.Month.ToString("D2");
                var tradePath = Path.Combine(_imagesPath, year, month, tradeId.ToString());
                Directory.CreateDirectory(tradePath);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileInfo.Extension}";
                var destPath = Path.Combine(tradePath, fileName);

                // Copy image
                await Task.Run(() => File.Copy(sourcePath, destPath));

                // Create thumbnail
                var thumbnailPath = await CreateThumbnailAsync(destPath, tradeId);

                result.Success = true;
                result.FileName = fileName;
                result.FilePath = GetRelativePath(destPath);
                result.ThumbnailPath = GetRelativePath(thumbnailPath);
                result.FileSize = fileInfo.Length;
                result.Message = "تصویر با موفقیت ذخیره شد";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در ذخیره تصویر: {ex.Message}";
            }

            return result;
        }

        public async Task<string> CreateThumbnailAsync(string imagePath, int tradeId)
        {
            return await Task.Run(() =>
            {
                using var image = Image.FromFile(imagePath);
                
                // Calculate thumbnail dimensions
                int width, height;
                if (image.Width > image.Height)
                {
                    width = _thumbnailSize;
                    height = (int)(image.Height * ((float)_thumbnailSize / image.Width));
                }
                else
                {
                    height = _thumbnailSize;
                    width = (int)(image.Width * ((float)_thumbnailSize / image.Height));
                }

                // Create thumbnail
                using var thumbnail = image.GetThumbnailImage(width, height, null, IntPtr.Zero);
                
                // Save thumbnail
                var year = DateTime.Now.Year.ToString();
                var month = DateTime.Now.Month.ToString("D2");
                var thumbnailDir = Path.Combine(_thumbnailsPath, year, month, tradeId.ToString());
                Directory.CreateDirectory(thumbnailDir);
                
                var thumbnailName = $"thumb_{Path.GetFileNameWithoutExtension(imagePath)}.jpg";
                var thumbnailPath = Path.Combine(thumbnailDir, thumbnailName);
                
                thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                
                return thumbnailPath;
            });
        }

        public async Task<bool> DeleteImageAsync(string filePath)
        {
            try
            {
                var fullPath = GetFullPath(filePath);
                
                if (File.Exists(fullPath))
                {
                    await Task.Run(() => File.Delete(fullPath));
                    
                    // Delete thumbnail
                    var thumbnailPath = GetThumbnailPath(fullPath);
                    if (File.Exists(thumbnailPath))
                    {
                        await Task.Run(() => File.Delete(thumbnailPath));
                    }
                    
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetTradeImagesAsync(int tradeId)
        {
            var images = new List<string>();
            
            var year = DateTime.Now.Year.ToString();
            var month = DateTime.Now.Month.ToString("D2");
            var tradePath = Path.Combine(_imagesPath, year, month, tradeId.ToString());
            
            if (Directory.Exists(tradePath))
            {
                var files = await Task.Run(() => Directory.GetFiles(tradePath));
                images.AddRange(files.Select(f => GetRelativePath(f)));
            }
            
            // Check previous months too
            for (int i = 1; i <= 12; i++)
            {
                var checkPath = Path.Combine(_imagesPath, year, i.ToString("D2"), tradeId.ToString());
                if (Directory.Exists(checkPath))
                {
                    var files = await Task.Run(() => Directory.GetFiles(checkPath));
                    images.AddRange(files.Select(f => GetRelativePath(f)));
                }
            }
            
            return images;
        }

        public BitmapImage LoadImage(string relativePath)
        {
            try
            {
                var fullPath = GetFullPath(relativePath);
                if (!File.Exists(fullPath))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ImageOptimizeResult> OptimizeImageAsync(string imagePath, int quality = 85)
        {
            var result = new ImageOptimizeResult();
            
            try
            {
                var fullPath = GetFullPath(imagePath);
                var originalSize = new FileInfo(fullPath).Length;
                
                await Task.Run(() =>
                {
                    using var image = Image.FromFile(fullPath);
                    
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    
                    var jpegCodec = ImageCodecInfo.GetImageDecoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    
                    var tempPath = fullPath + ".tmp";
                    image.Save(tempPath, jpegCodec, encoderParams);
                    
                    File.Delete(fullPath);
                    File.Move(tempPath, fullPath);
                });
                
                var newSize = new FileInfo(fullPath).Length;
                
                result.Success = true;
                result.OriginalSize = originalSize;
                result.OptimizedSize = newSize;
                result.SizeReduction = ((originalSize - newSize) / (double)originalSize) * 100;
                result.Message = $"تصویر {result.SizeReduction:F1}% کوچکتر شد";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در بهینه‌سازی: {ex.Message}";
            }
            
            return result;
        }

        private string GetRelativePath(string fullPath)
        {
            return fullPath.Replace(_imagesPath + Path.DirectorySeparatorChar, "");
        }

        private string GetFullPath(string relativePath)
        {
            return Path.Combine(_imagesPath, relativePath);
        }

        private string GetThumbnailPath(string imagePath)
        {
            var relativePath = GetRelativePath(imagePath);
            var fileName = $"thumb_{Path.GetFileNameWithoutExtension(relativePath)}.jpg";
            var dir = Path.GetDirectoryName(relativePath);
            return Path.Combine(_thumbnailsPath, dir, fileName);
        }
    }

    public class ImageSaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ThumbnailPath { get; set; }
        public long FileSize { get; set; }
    }

    public class ImageOptimizeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long OriginalSize { get; set; }
        public long OptimizedSize { get; set; }
        public double SizeReduction { get; set; }
    }
}
// پایان کد