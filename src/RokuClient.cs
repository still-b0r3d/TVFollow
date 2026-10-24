using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace TVFollow
{
    // What the TV says about itself (GET /query/device-info).
    class DeviceInfo
    {
        public string Serial = "";
        public string Name = "";
        public string Location = "";
        public string Vendor = "";
        public string Model = "";
        public string PowerMode = "";
        public string NetworkType = "";
        public string WifiMac = "";
        public string EthernetMac = "";
        public string ControlMode = "";
        public bool IsTv;

        // the address to wake: the TV's cable one if it's plugged in, else its Wi-Fi one
        public string WakeMac { get { return NetworkType == "ethernet" ? EthernetMac : WifiMac; } }

        // "Control by mobile apps" set to Limited blocks the power and input buttons.
        // Older TVs don't report the setting at all and allow everything.
        public bool AllowsControl { get { return ControlMode != "limited" && ControlMode != "disabled"; } }

        public bool IsOn { get { return PowerMode == "PowerOn"; } }

        public static DeviceInfo Parse(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var d = new DeviceInfo();
            d.Serial = Field(doc, "serial-number");
            d.Name = FirstOf(Field(doc, "user-device-name"), Field(doc, "friendly-device-name"), Field(doc, "default-device-name"));
            d.Location = Field(doc, "user-device-location");
            d.Vendor = Field(doc, "vendor-name");
            d.Model = Field(doc, "model-name");
            d.PowerMode = Field(doc, "power-mode");
            d.NetworkType = Field(doc, "network-type");
            d.WifiMac = Field(doc, "wifi-mac");
            d.EthernetMac = Field(doc, "ethernet-mac");
            d.ControlMode = Field(doc, "ecp-setting-mode");
            d.IsTv = Field(doc, "is-tv") == "true";
            return d;
        }

        static string Field(XmlDocument doc, string name)
        {
            var node = doc.SelectSingleNode("/device-info/" + name);
            return node == null ? "" : node.InnerText.Trim();
        }

        static string FirstOf(params string[] values)
        {
            foreach (string v in values) if (v.Length > 0) return v;
            return "";
        }
    }

    // Talks to one TV over Roku's External Control Protocol: plain HTTP on port 8060.
    class RokuClient
    {
        public string Ip;
        public int Port;

        public RokuClient(string ip, int port)
        {
            Ip = ip;
            Port = port;
        }

        // null if the TV didn't answer
        public DeviceInfo GetDeviceInfo(int timeoutMs)
        {
            try { return DeviceInfo.Parse(Get("query/device-info", timeoutMs)); }
            catch { return null; }
        }

        // the app on screen ("tvinput.hdmi1" for an HDMI input), "" for the Roku home screen,
        // null if the TV didn't answer
        public string GetActiveApp(int timeoutMs)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(Get("query/active-app", timeoutMs));
                var app = doc.SelectSingleNode("/active-app/app") as XmlElement;
                return app == null ? "" : app.GetAttribute("id");
            }
            catch { return null; }
        }

        // The TV's HDMI inputs as (id, name), like ("tvinput.hdmi1", "PC"). Null if the TV didn't
        // answer or won't say (the Limited setting hides the list).
        public List<KeyValuePair<string, string>> GetHdmiInputs(int timeoutMs)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(Get("query/apps", timeoutMs));
                var inputs = new List<KeyValuePair<string, string>>();
                foreach (XmlElement app in doc.SelectNodes("/apps/app"))
                {
                    string id = app.GetAttribute("id");
                    if (!id.StartsWith("tvinput.hdmi")) continue;
                    inputs.Add(new KeyValuePair<string, string>(id, app.InnerText.Replace(' ', ' ').Trim()));
                }
                inputs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                return inputs;
            }
            catch { return null; }
        }

        public int Keypress(string key, int timeoutMs) { return Post("keypress/" + key, timeoutMs); }

        public int Launch(string appId, int timeoutMs) { return Post("launch/" + appId, timeoutMs); }

        HttpWebRequest Request(string path, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create("http://" + Ip + ":" + Port + "/" + path);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.Proxy = null;
            req.KeepAlive = false;
            return req;
        }

        string Get(string path, int timeoutMs)
        {
            using (var resp = (HttpWebResponse)Request(path, timeoutMs).GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        // the HTTP status, or 0 if the TV didn't answer
        int Post(string path, int timeoutMs)
        {
            HttpWebRequest req;
            try { req = Request(path, timeoutMs); }
            catch { return 0; }
            req.Method = "POST";
            req.ContentLength = 0;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse()) return (int)resp.StatusCode;
            }
            catch (WebException e)
            {
                var resp = e.Response as HttpWebResponse;
                if (resp == null) return 0;
                int code = (int)resp.StatusCode;
                resp.Close();
                return code;
            }
        }
    }
}
