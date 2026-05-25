using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 本地缩略图缓存。将大尺寸 BMP 原图解码缩放到指定宽度，编码为 JPEG 存入
    /// %LocalAppData%/ZenergyBFSI/thumbcache/，后续请求直接返回缓存文件路径。
    /// 缓存键 = SHA256(源文件绝对路径 + 最后修改时间 Ticks)，源文件更新自动重建。
    /// </summary>
    public static class ThumbnailCache
    {
        private static readonly string CacheDir;
        private static readonly ConcurrentDictionary<string, object> _locks =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        static ThumbnailCache()
        {
            CacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZenergyBFSI", "thumbcache");
            Directory.CreateDirectory(CacheDir);
        }

        /// <summary>
        /// 获取或创建缩略图。返回缓存 JPEG 文件的完整路径。
        /// 首次调用时从原图解码降采样并编码写入缓存；后续命中直接返回路径。
        /// </summary>
        /// <param name="sourcePath">原图 BMP 绝对路径</param>
        /// <param name="decodeWidth">解码目标宽度（像素）</param>
        /// <returns>缓存文件路径，失败返回 null</returns>
        public static string GetOrCreate(string sourcePath, int decodeWidth)
        {
            if (!File.Exists(sourcePath)) return null;

            var cacheKey = ComputeCacheKey(sourcePath, decodeWidth);
            var cacheFile = Path.Combine(CacheDir, cacheKey + ".jpg");

            if (File.Exists(cacheFile)) return cacheFile;

            // 同源文件只允许一个线程解码（避免重复 IO）
            var keyLock = _locks.GetOrAdd(cacheKey, _ => new object());
            lock (keyLock)
            {
                if (File.Exists(cacheFile)) return cacheFile; // 双重检查

                try
                {
                    return BuildThumbnail(sourcePath, decodeWidth, cacheFile);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is NotSupportedException || ex is IOException || ex is InvalidOperationException)
                {
                    return null;
                }
            }
        }

        /// <summary>清除全部缓存文件。</summary>
        public static void Clear()
        {
            try
            {
                foreach (var f in Directory.GetFiles(CacheDir, "*.jpg"))
                    File.Delete(f);
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════
        //  内部
        // ════════════════════════════════════════════════════════

        private static string ComputeCacheKey(string sourcePath, int decodeWidth)
        {
            var raw = sourcePath + "|" + File.GetLastWriteTime(sourcePath).Ticks + "|" + decodeWidth;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string BuildThumbnail(string sourcePath, int decodeWidth, string cacheFile)
        {
            // 1. 解码 BMP 到缩略尺寸
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(sourcePath, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.EndInit();
            bmp.Freeze();

            // 2. 编码为 JPEG 写入缓存
            JpegBitmapEncoder encoder = new JpegBitmapEncoder
            {
                QualityLevel = 85
            };
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            try
            {
                using (var fs = new FileStream(cacheFile, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }
            }
            catch (IOException)
            {
                // 写入失败，删除不完整文件，下次重新解码
                try { File.Delete(cacheFile); } catch { }
                return null;
            }

            return cacheFile;
        }
    }
}
