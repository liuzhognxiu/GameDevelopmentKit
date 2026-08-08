using System;
using System.IO;
using System.Text;

namespace Game.Hot.Buqi.Run.Settlement
{
    public sealed class BuqiFileRunStore : IBuqiRunStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string m_Path;

        public BuqiFileRunStore(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException("Save path is required.", nameof(absolutePath));
            }

            if (!Path.IsPathRooted(absolutePath))
            {
                throw new ArgumentException("Save path must be absolute.", nameof(absolutePath));
            }

            m_Path = Path.GetFullPath(absolutePath);
        }

        public bool TryRead(out string json, out string error)
        {
            try
            {
                if (!File.Exists(m_Path))
                {
                    json = string.Empty;
                    error = "Save file does not exist.";
                    return false;
                }

                json = File.ReadAllText(m_Path, Utf8NoBom);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                json = string.Empty;
                error = exception.Message;
                return false;
            }
        }

        public bool TryWrite(string json, out string error)
        {
            string tmpPath = m_Path + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           tmpPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    writer.Write(json ?? string.Empty);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(m_Path))
                {
                    File.Replace(tmpPath, m_Path, null);
                }
                else
                {
                    File.Move(tmpPath, m_Path);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteFile(tmpPath);
                error = exception.Message;
                return false;
            }
        }

        public bool TryDelete(out string error)
        {
            try
            {
                TryDeleteFile(m_Path);
                TryDeleteFile(m_Path + ".tmp");
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
