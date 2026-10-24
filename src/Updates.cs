using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // The parts of GitHub's "latest release" answer that matter here.
    [DataContract]
    class Release
    {
        [DataMember(Name = "tag_name")] public string Tag;
        [DataMember(Name = "html_url")] public string Url;
        [DataMember(Name = "draft")] public bool Draft;
        [DataMember(Name = "prerelease")] public bool Prerelease;

        public string Version { get { return (Tag ?? "").TrimStart('v', 'V'); } }

        public static Release Parse(string json)
        {
            var reader = new DataContractJsonSerializer(typeof(Release));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (Release)reader.ReadObject(stream);
        }
    }

    // Once a week, when the setting is on, TVFollow asks GitHub for its latest release. A newer
    // one gets a notification and a menu item linking to its page. Nothing is downloaded or
    // installed: that stays the person's call.
    static class Updates
    {
        public const string LatestUrl = "https://api.github.com/repos/still-b0r3d/TVFollow/releases/latest";
        public static readonly TimeSpan Every = TimeSpan.FromDays(7);
        public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(6);

        public static string FilePath { get { return Path.Combine(Settings.Dir, "updates.ini"); } }

        // "1.1" newer than "1.0"? Numbers compared one by one, so "1.10" beats "1.9".
        public static bool IsNewer(string candidate, string current)
        {
            int[] a = Numbers(candidate), b = Numbers(current);
            if (a == null || b == null) return false;
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        static int[] Numbers(string version)
        {
            string[] parts = (version ?? "").Trim().TrimStart('v', 'V').Split('.');
            var numbers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return null;
            return numbers;
        }

        public static bool IsDue(DateTime lastCheckUtc, DateTime nowUtc)
        {
            return nowUtc - lastCheckUtc >= Every || lastCheckUtc > nowUtc; // a clock set back counts as due
        }

        // only ever open TVFollow's own release pages
        public static bool IsOurPage(string url)
        {
            return url != null && url.StartsWith(SetupForm.HelpUrl + "/releases/", StringComparison.Ordinal);
        }

        // The latest release; one with a null Tag if there's no release yet; null if GitHub
        // couldn't be reached.
        public static Release FetchLatest()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; // GitHub needs TLS 1.2
                var req = (HttpWebRequest)WebRequest.Create(LatestUrl);
                req.UserAgent = "TVFollow/" + Program.Version;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return Release.Parse(reader.ReadToEnd());
            }
            catch (WebException e)
            {
                var resp = e.Response as HttpWebResponse;
                bool none = resp != null && resp.StatusCode == HttpStatusCode.NotFound;
                if (resp != null) resp.Close();
                return none ? new Release() : null;
            }
            catch { return null; }
        }

        // updates.ini: when GitHub was last asked, and the newest version it named
        public static void Load(out DateTime lastCheckUtc, out string latest, out string latestUrl)
        {
            lastCheckUtc = DateTime.MinValue;
            latest = latestUrl = "";
            if (!File.Exists(FilePath)) return;
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                string key = raw.Substring(0, eq).Trim(), value = raw.Substring(eq + 1).Trim();
                if (key == "last_check")
                    DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out lastCheckUtc);
                else if (key == "latest") latest = value;
                else if (key == "latest_url") latestUrl = value;
            }
        }

        public static void Save(DateTime lastCheckUtc, string latest, string latestUrl)
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(FilePath,
                "# TVFollow's update check. Delete this file to check again right away.\r\n" +
                "last_check=" + lastCheckUtc.ToString("o", CultureInfo.InvariantCulture) + "\r\n" +
                "latest=" + latest + "\r\n" +
                "latest_url=" + latestUrl + "\r\n");
        }
    }

    // The weekly check inside the running copy: first a couple of minutes after it starts, then
    // it looks again every hour whether a week has passed.
    class UpdateWatcher
    {
        readonly Tray tray;
        readonly Timer timer = new Timer();
        DateTime lastTry = DateTime.MinValue;
        bool checking;

        public UpdateWatcher(Tray tray)
        {
            this.tray = tray;

            // a newer version found before this copy started is still worth a menu item
            DateTime last;
            string latest, url;
            Updates.Load(out last, out latest, out url);
            if (Updates.IsNewer(latest, Program.Version) && Updates.IsOurPage(url)) tray.ShowUpdate(latest, url, false);

            timer.Interval = 2 * 60 * 1000;
            timer.Tick += delegate { timer.Interval = 60 * 60 * 1000; Check(); };
            timer.Start();
        }

        async void Check()
        {
            DateTime last;
            string latest, url;
            Updates.Load(out last, out latest, out url);
            DateTime now = DateTime.UtcNow;
            if (checking || !Updates.IsDue(last, now) || now - lastTry < Updates.RetryAfter) return;
            checking = true;
            lastTry = now;
            Release release = await Task.Run(() => Updates.FetchLatest());
            checking = false;

            if (release == null) { Log.Write("update check: couldn't reach GitHub, trying again later"); return; }
            if (release.Tag == null) { Log.Write("update check: no releases yet"); Updates.Save(now, latest, url); return; }
            Log.Write("update check: the latest release is " + release.Tag);
            if (!release.Draft && !release.Prerelease && Updates.IsNewer(release.Version, Program.Version) && Updates.IsOurPage(release.Url))
            {
                tray.ShowUpdate(release.Version, release.Url, release.Version != latest); // tell once per version
                latest = release.Version;
                url = release.Url;
            }
            Updates.Save(now, latest, url);
        }
    }
}
