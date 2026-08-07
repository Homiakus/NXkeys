using System.Text;

namespace NxEskd.Core.Utilities;

/// <summary>Записывает файл через временный файл в том же каталоге и, при необходимости, сохраняет предыдущую версию.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content, bool createBackup = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("Не удалось определить каталог файла.");
        Directory.CreateDirectory(directory);

        var temp = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024,
                       FileOptions.WriteThrough | FileOptions.SequentialScan))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(target))
            {
                File.Move(temp, target);
                return;
            }

            var backup = createBackup ? target + ".bak" : null;
            try
            {
                File.Replace(temp, target, backup, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceFallback(temp, target, backup);
            }
            catch (IOException)
            {
                ReplaceFallback(temp, target, backup);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void ReplaceFallback(string temp, string target, string? backup)
    {
        if (backup is not null) File.Copy(target, backup, overwrite: true);
        File.Move(temp, target, overwrite: true);
    }
}
