using System;
using System.IO;

namespace TVFollow
{
    // What TVFollow did and when, in %APPDATA%\TVFollow\TVFollow.log (kept under about 256 KB).
    static class Log
    {
        static readonly object Lock = new object();

        public static string FilePath { get { return Path.Combine(Settings.Dir, "TVFollow.log"); } }

        public static void Write(string message)
        {
            lock (Lock)
            {
                try
                {
                    Directory.CreateDirectory(Settings.Dir);
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > 256 * 1024)
                    {
                        string old = FilePath + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
