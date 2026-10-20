using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace CCleaner
{
    public class UpdateInfo
    {
        public string Version = "";
        public string Url = "";
        public string Notes = "";
    }

    public class UpdateState
    {
        public bool AutoCheck = true;
        public string IgnoredVersion = "";
        public DateTime LastCheckUtc = DateTime.MinValue;
        public string LatestVersion = "";
        public string LatestUrl = "";
    }

    public static class UpdateStore
    {
        public static string StateDir(string localAppData)
        {
            return Path.Combine(localAppData, "C盘清理助手");
        }

        public static string StatePath(string localAppData)
        {
            return Path.Combine(StateDir(localAppData), "update-state.json");
        }

        public static UpdateState Load(string localAppData)
        {
            UpdateState s = new UpdateState();
            try
            {
                string path = StatePath(localAppData);
                if (!File.Exists(path)) return s;

                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> d = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                if (d == null) return s;

                object v;
                if (d.TryGetValue("autoCheck", out v) && v != null) s.AutoCheck = Convert.ToBoolean(v);
                if (d.TryGetValue("ignoredVersion", out v) && v != null) s.IgnoredVersion = Convert.ToString(v);
                if (d.TryGetValue("latestVersion", out v) && v != null) s.LatestVersion = Convert.ToString(v);
                if (d.TryGetValue("latestUrl", out v) && v != null) s.LatestUrl = Convert.ToString(v);
                if (d.TryGetValue("lastCheckUtc", out v) && v != null)
                {
                    DateTime t;
                    if (DateTime.TryParse(Convert.ToString(v), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out t))
                        s.LastCheckUtc = t.ToUniversalTime();
                }
            }
            catch { }
            return s;
        }

        public static void Save(string localAppData, UpdateState s)
        {
            try
            {
                string dir = StateDir(localAppData);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Dictionary<string, object> d = new Dictionary<string, object>();
                d["autoCheck"] = s.AutoCheck;
                d["ignoredVersion"] = s.IgnoredVersion;
                d["latestVersion"] = s.LatestVersion;
                d["latestUrl"] = s.LatestUrl;
                d["lastCheckUtc"] = s.LastCheckUtc == DateTime.MinValue
                    ? "" : s.LastCheckUtc.ToString("o");

                JavaScriptSerializer ser = new JavaScriptSerializer();
                File.WriteAllText(StatePath(localAppData), ser.Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    public static class UpdateChecker
    {
        public const string DefaultApi = "https://api.github.com/repos/Misaka-11736/c-drive-cleaner/releases/latest";

        /// <summary>测试时可指向本地 HTTP 服务。</summary>
        public static string ApiUrl()
        {
            string env = Environment.GetEnvironmentVariable("CCLEANER_UPDATE_URL");
            return string.IsNullOrEmpty(env) ? DefaultApi : env;
        }

        /// <summary>返回需要提示的新版本；没有更新、被忽略、关闭检查或网络失败时返回 null。</summary>
        public static UpdateInfo Check(string localAppData, bool force)
        {
            UpdateState state = UpdateStore.Load(localAppData);

            if (!force)
            {
                if (!state.AutoCheck) return null;
                if (state.LastCheckUtc != DateTime.MinValue &&
                    (DateTime.UtcNow - state.LastCheckUtc).TotalHours < 24) return null;
            }

            UpdateInfo info = Fetch();

            state.LastCheckUtc = DateTime.UtcNow;
            if (info != null)
            {
                state.LatestVersion = info.Version;
                state.LatestUrl = info.Url;
            }
            UpdateStore.Save(localAppData, state);

            if (info == null) return null;
            if (!IsNewer(info.Version, VersionInfo.Version)) return null;
            if (string.Equals(info.Version, state.IgnoredVersion, StringComparison.OrdinalIgnoreCase)) return null;
            return info;
        }

        public static void Ignore(string localAppData, string version)
        {
            UpdateState state = UpdateStore.Load(localAppData);
            state.IgnoredVersion = version;
            UpdateStore.Save(localAppData, state);
        }

        public static UpdateInfo Fetch()
        {
            UpdateInfo info = FetchFromApi();
            if (info != null) return info;
            return FetchFromReleasePage();
        }

        /// <summary>
        /// 部分网络（企业代理/地区限制）会拦截 api.github.com，但 github.com 可访问。
        /// 此时跟随 releases/latest 的 302 跳转，从最终 URL 的 /tag/vX.Y.Z 取版本。
        /// </summary>
        public static UpdateInfo FetchFromReleasePage()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    "https://github.com/Misaka-11736/c-drive-cleaner/releases/latest");
                req.UserAgent = "C-Drive-Cleaner/" + VersionInfo.Version;
                req.Timeout = 8000;
                req.ReadWriteTimeout = 8000;
                req.AllowAutoRedirect = true;
                req.Proxy = WebRequest.DefaultWebProxy;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string uri = resp.ResponseUri == null ? "" : resp.ResponseUri.ToString();
                    int idx = uri.LastIndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) return null;

                    string tag = uri.Substring(idx + 5).Trim().TrimStart('v', 'V');
                    if (tag.Length == 0) return null;

                    UpdateInfo info = new UpdateInfo();
                    info.Version = tag;
                    info.Url = uri;
                    return info;
                }
            }
            catch
            {
                return null;
            }
        }

        private static UpdateInfo FetchFromApi()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ApiUrl());
                req.UserAgent = "C-Drive-Cleaner/" + VersionInfo.Version;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.Proxy = WebRequest.DefaultWebProxy;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> d = ser.Deserialize<Dictionary<string, object>>(json);
                    if (d == null) return null;

                    object v;
                    if (!d.TryGetValue("tag_name", out v) || v == null) return null;

                    if (d.TryGetValue("prerelease", out v) && v != null && Convert.ToBoolean(v)) return null;
                    if (d.TryGetValue("draft", out v) && v != null && Convert.ToBoolean(v)) return null;

                    UpdateInfo info = new UpdateInfo();
                    info.Version = Convert.ToString(d["tag_name"]).Trim().TrimStart('v', 'V');
                    if (d.TryGetValue("html_url", out v) && v != null) info.Url = Convert.ToString(v);
                    if (d.TryGetValue("body", out v) && v != null) info.Notes = Convert.ToString(v);
                    return info;
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool IsNewer(string remote, string local)
        {
            if (string.IsNullOrEmpty(remote)) return false;
            int[] a = ParseVersion(remote);
            int[] b = ParseVersion(local);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
            return false;
        }

        private static int[] ParseVersion(string v)
        {
            int[] r = new int[3];
            if (string.IsNullOrEmpty(v)) return r;

            int cut = v.IndexOfAny(new char[] { '-', '+', ' ' });
            string core = cut > 0 ? v.Substring(0, cut) : v;
            string[] parts = core.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                int n;
                int.TryParse(parts[i], out n);
                r[i] = n;
            }
            return r;
        }
    }
}
