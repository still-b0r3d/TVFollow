using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace TVFollow
{
    // why a folder or a zip can't be pushed, in words for the window
    class DevPackageException : Exception
    {
        public DevPackageException(string message) : base(message) { }
    }

    // An app ready to push: the zip Roku's installer takes, with the manifest at its top and
    // paths with forward slashes, and the name and version its manifest gives. Made the same
    // way from a folder, from a zip (made again, since zips from some tools use backslashes)
    // and from the example app built into TVFollow, and the same files always make the same
    // zip, so the TV can tell when nothing changed.
    class DevPackage
    {
        // far past any Roku app; it only keeps a wrong pick (a whole drive, a video) out of memory
        public const long MaxBytes = 200L * 1024 * 1024;
        public const string ExamplePrefix = "example/";

        public byte[] Zip;
        public string Title = "";
        public string Version = "";
        public string Root = "";   // the folder the app came from ("" for a zip or the example)
        public string Note = "";   // e.g. the app was one folder down
        public int Files;

        public override string ToString()
        {
            return (Title.Length > 0 ? Title : "the app") + (Version.Length > 0 ? " " + Version : "");
        }

        class Item
        {
            public string Name;            // the path inside the app, with forward slashes
            public DateTime Time;
            public Func<byte[]> Read;
        }

        // What's left out: hidden things (.git, .vscode, .DS_Store), what macOS adds to zips,
        // Windows' Thumbs.db and desktop.ini, and editors' temporary and backup files.
        public static bool Skipped(string relativePath)
        {
            foreach (string part in relativePath.Split('/', '\\'))
            {
                if (part.Length == 0) continue;
                string lower = part.ToLowerInvariant();
                if (lower.StartsWith(".") || lower.StartsWith("~$") || lower.EndsWith("~") || lower == "__macosx" ||
                    lower == "thumbs.db" || lower == "desktop.ini" || lower.EndsWith(".tmp") || lower.EndsWith(".swp") || lower.EndsWith(".bak"))
                    return true;
            }
            return false;
        }

        // An app's folder: the one with the manifest file in it. A folder with one app folder
        // inside it does too (picking the folder around it is an easy slip).
        public static DevPackage FromFolder(string folder)
        {
            if (!Directory.Exists(folder)) throw new DevPackageException("That folder isn't there any more: " + folder);
            string root = Path.GetFullPath(folder).TrimEnd('\\');
            string note = "";
            if (!File.Exists(Path.Combine(root, "manifest")))
            {
                var inside = new List<string>();
                foreach (string sub in Directory.GetDirectories(root))
                    if (!Skipped(Path.GetFileName(sub)) && File.Exists(Path.Combine(sub, "manifest"))) inside.Add(sub);
                if (inside.Count > 1) throw new DevPackageException("That folder has more than one app in it. Pick the app's own folder, the one with the manifest file.");
                if (inside.Count == 0) throw new DevPackageException(NoManifest("folder"));
                root = inside[0];
                note = "The app was in the folder " + Path.GetFileName(root) + " inside it, so that's what went.";
            }
            var items = new List<Item>();
            long total = 0;
            Walk(root, "", 0, items, ref total);
            DevPackage app = Pack(items, note);
            app.Root = root;
            return app;
        }

        // a Roku app is a few folders deep and has dozens of files, hundreds at most
        const int MaxDepth = 32, MaxFiles = 20000;

        static void Walk(string dir, string prefix, int depth, List<Item> items, ref long total)
        {
            // a link back up the tree (a junction, say) would otherwise go round for ever. Links
            // can't simply be skipped: OneDrive marks its folders the same way.
            if (depth > MaxDepth) throw new DevPackageException("That folder goes too many folders deep for a Roku app. Pick the app's own folder.");
            foreach (string file in Directory.GetFiles(dir))
            {
                string relative = prefix + Path.GetFileName(file);
                if (Skipped(relative)) continue;
                var info = new FileInfo(file);
                total += info.Length;
                if (total > MaxBytes || items.Count >= MaxFiles) throw new DevPackageException(TooBig());
                string path = file;
                items.Add(new Item { Name = relative, Time = info.LastWriteTime, Read = () => File.ReadAllBytes(path) });
            }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (!Skipped(name)) Walk(sub, prefix + name + "/", depth + 1, items, ref total);
            }
        }

        // A zip of the app. One with the app in a folder of its own inside works too.
        public static DevPackage FromZip(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new DevPackageException("That file isn't there any more: " + path);
            if (file.Length > MaxBytes) throw new DevPackageException(TooBig());
            byte[] bytes = File.ReadAllBytes(path);
            var items = new List<Item>();
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                {
                    long total = 0;
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');
                        if (name.Length == 0 || name.EndsWith("/") || Skipped(name)) continue;   // a folder, or junk
                        if (name.StartsWith("/") || name.Contains(":") || ("/" + name + "/").Contains("/../")) continue;
                        total += entry.Length;
                        if (total > MaxBytes) throw new DevPackageException(TooBig());
                        byte[] content = ReadEntry(entry);
                        items.Add(new Item { Name = name, Time = entry.LastWriteTime.DateTime, Read = () => content });
                    }
                }
            }
            catch (InvalidDataException) { throw new DevPackageException("That file isn't a zip Windows can open."); }
            string note = "";
            if (!items.Exists(i => i.Name == "manifest"))
            {
                // everything in one folder, with the manifest at that folder's top
                int slash = items.Count > 0 ? items[0].Name.IndexOf('/') : -1;
                string top = slash > 0 ? items[0].Name.Substring(0, slash + 1) : null;
                if (top == null || !items.TrueForAll(i => i.Name.StartsWith(top)) || !items.Exists(i => i.Name == top + "manifest"))
                    throw new DevPackageException(NoManifest("zip"));
                foreach (Item i in items) i.Name = i.Name.Substring(top.Length);
                note = "The app was in the folder " + top.TrimEnd('/') + " inside the zip, so that's what went.";
            }
            return Pack(items, note);
        }

        static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            using (Stream s = entry.Open())
            {
                var all = new MemoryStream();
                var buffer = new byte[65536];
                int n;
                while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                    all.Write(buffer, 0, n);
                    // the size a zip claims can lie; the real one counts
                    if (all.Length > MaxBytes) throw new DevPackageException(TooBig());
                }
                return all.ToArray();
            }
        }

        // the example app, built into TVFollow file by file
        public static DevPackage Example()
        {
            Assembly me = typeof(DevPackage).Assembly;
            var items = new List<Item>();
            foreach (string name in me.GetManifestResourceNames())
            {
                if (!name.StartsWith(ExamplePrefix)) continue;
                string resource = name;
                items.Add(new Item
                {
                    Name = name.Substring(ExamplePrefix.Length),
                    Time = new DateTime(2026, 11, 6, 12, 0, 0),
                    Read = () =>
                    {
                        using (Stream s = me.GetManifestResourceStream(resource))
                        {
                            var all = new MemoryStream();
                            s.CopyTo(all);
                            return all.ToArray();
                        }
                    }
                });
            }
            if (!items.Exists(i => i.Name == "manifest")) throw new DevPackageException("This TVFollow was built without its example app.");
            return Pack(items, "");
        }

        // the name and version of the app in a zip made here (the copy kept to put back)
        public static DevPackage Describe(byte[] zip)
        {
            var app = new DevPackage { Zip = zip };
            try
            {
                using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                {
                    ZipArchiveEntry manifest = archive.GetEntry("manifest");
                    if (manifest != null && manifest.Length < 64 * 1024) ReadManifest(Encoding.UTF8.GetString(ReadEntry(manifest)), app);
                    app.Files = archive.Entries.Count;
                }
            }
            catch { }
            return app;
        }

        static DevPackage Pack(List<Item> items, string note)
        {
            // in name order, so the same files always make the same zip
            items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var app = new DevPackage { Note = note };
            var output = new MemoryStream();
            long total = 0;
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (Item item in items)
                {
                    byte[] content = item.Read();
                    total += content.Length;
                    if (total > MaxBytes) throw new DevPackageException(TooBig());
                    ZipArchiveEntry entry = zip.CreateEntry(item.Name, CompressionLevel.Optimal);
                    entry.LastWriteTime = ZipTime(item.Time);
                    using (Stream s = entry.Open()) s.Write(content, 0, content.Length);
                    if (item.Name == "manifest") ReadManifest(Encoding.UTF8.GetString(content), app);
                    app.Files++;
                }
            }
            app.Zip = output.ToArray();
            return app;
        }

        // a zip can only hold times from 1980 to 2107
        static DateTimeOffset ZipTime(DateTime time)
        {
            if (time.Year < 1981) time = new DateTime(1981, 1, 1);
            if (time.Year > 2100) time = new DateTime(2100, 1, 1);
            return new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), TimeSpan.Zero);
        }

        // the manifest's title and its major_version.minor_version.build_version
        static void ReadManifest(string text, DevPackage app)
        {
            string major = "", minor = "", build = "";
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.StartsWith("#") || eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant(), value = line.Substring(eq + 1).Trim();
                if (key == "title") app.Title = value;
                else if (key == "major_version") major = value;
                else if (key == "minor_version") minor = value;
                else if (key == "build_version") build = value;
            }
            if (major.Length > 0) app.Version = major + "." + (minor.Length > 0 ? minor : "0") + (build.Length > 0 ? "." + build : "");
        }

        static string NoManifest(string what)
        {
            return "There's no manifest file at the top of that " + what + ". A Roku app's own folder has a file named manifest, " +
                "with its source folder beside it.";
        }

        static string TooBig()
        {
            return "That's far too big for a Roku app (over " + Size(MaxBytes) + "). Pick the app's own folder.";
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " bytes";
            if (bytes < 1024 * 1024) return (bytes + 1023) / 1024 + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
        }
    }
}
