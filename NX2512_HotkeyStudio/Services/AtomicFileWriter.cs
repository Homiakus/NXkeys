using System;
using System.IO;
using System.Text;
using System.Threading;

namespace NX2512_HotkeyStudio.Services
{
    public static class AtomicFileWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void WriteAllText(string path, string content, bool atomic = true, Encoding encoding = null)
        {
            WriteAllBytes(path, (encoding ?? Utf8NoBom).GetBytes(content ?? string.Empty), atomic);
        }

        public static void CopyFile(string source, string destination, bool atomic = true)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException("Исходный файл не найден.", source);
            WriteAllBytes(destination, File.ReadAllBytes(source), atomic);
        }

        public static void WriteAllBytes(string path, byte[] content, bool atomic = true)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Путь назначения не задан.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Не удалось определить каталог назначения: " + fullPath);
            Directory.CreateDirectory(directory);

            if (!atomic)
            {
                File.WriteAllBytes(fullPath, content ?? Array.Empty<byte>());
                return;
            }

            Exception lastError = null;
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                string token = Guid.NewGuid().ToString("N");
                string tempPath = Path.Combine(directory, ".nxkeys-" + token + ".tmp");
                string rollbackPath = Path.Combine(directory, ".nxkeys-" + token + ".rollback");
                try
                {
                    using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
                    {
                        byte[] bytes = content ?? Array.Empty<byte>();
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    bool hadOriginal = File.Exists(fullPath);
                    if (hadOriginal) File.Copy(fullPath, rollbackPath, true);

                    try
                    {
                        File.Move(tempPath, fullPath, true);
                    }
                    catch
                    {
                        if (hadOriginal && File.Exists(rollbackPath)) File.Copy(rollbackPath, fullPath, true);
                        throw;
                    }

                    TryDelete(rollbackPath);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    TryDelete(tempPath);
                    TryDelete(rollbackPath);
                    if (attempt < 8) Thread.Sleep(150 * attempt);
                }
            }

            throw new IOException("Не удалось атомарно записать файл после повторных попыток: " + fullPath, lastError);
        }

        public static void DeleteWithRetry(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            Exception lastError = null;
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 8) Thread.Sleep(150 * attempt);
                }
            }
            throw new IOException("Не удалось удалить устаревший файл: " + path, lastError);
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
