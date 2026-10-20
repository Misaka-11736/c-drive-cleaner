using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CCleaner
{
    public static class PlanStore
    {
        private static JavaScriptSerializer NewSerializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 100;
            return s;
        }

        public static void Save(ScanResult scan, string path)
        {
            List<object> items = new List<object>();
            foreach (CleanTarget t in scan.Targets)
            {
                if (!t.Exists) continue;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["id"] = t.Id;
                d["category"] = t.Cat;
                d["label"] = t.Label;
                d["mode"] = t.ModeText;
                d["risk"] = RiskText.Of(t.Risk);
                d["admin"] = t.Admin;
                d["recommend"] = t.Recommend;
                d["deletable"] = t.Deletable;
                d["bytes"] = t.Bytes;
                d["files"] = t.Files;
                d["paths"] = t.Paths.ToArray();
                d["denied"] = t.Denied;
                d["note"] = t.Note;
                d["lastWrite"] = t.LastWrite == DateTime.MinValue ? "" : t.LastWrite.ToString("yyyy-MM-dd HH:mm");
                items.Add(d);
            }

            Dictionary<string, object> drive = new Dictionary<string, object>();
            drive["total"] = scan.DriveTotal;
            drive["free"] = scan.DriveFree;

            Dictionary<string, object> totals = new Dictionary<string, object>();
            totals["cleanBytes"] = scan.CleanBytes;
            totals["recommendedBytes"] = scan.RecBytes;
            totals["reportOnlyBytes"] = scan.ReportBytes;

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["schema"] = "c-cleaner-plan/1";
            root["version"] = "2.0";
            root["generatedAt"] = scan.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss");
            root["elevated"] = scan.Elevated;
            root["machine"] = scan.Computer;
            root["user"] = scan.User;
            root["drive"] = drive;
            root["totals"] = totals;
            root["items"] = items;

            string json = NewSerializer().Serialize(root);
            json = Regex.Replace(json, @"\\u([0-9a-fA-F]{4})", delegate(Match m)
            {
                return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString();
            });

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir) && dir.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, json, new UTF8Encoding(true));
        }

        public static List<CleanTarget> Load(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            Dictionary<string, object> root = NewSerializer().Deserialize<Dictionary<string, object>>(text);
            if (root == null) throw new InvalidDataException("计划文件为空或格式不正确。");

            object schema;
            if (!root.TryGetValue("schema", out schema) || Convert.ToString(schema) != "c-cleaner-plan/1")
                throw new InvalidDataException("计划文件 schema 不匹配，拒绝导入。");

            object itemsObj;
            if (!root.TryGetValue("items", out itemsObj)) throw new InvalidDataException("计划文件缺少 items。");

            IList arr = itemsObj as IList;
            if (arr == null) throw new InvalidDataException("items 不是数组。");

            List<CleanTarget> list = new List<CleanTarget>();
            foreach (object o in arr)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;

                CleanTarget t = new CleanTarget();
                t.Id = Str(d, "id");
                t.Cat = Str(d, "category");
                t.Label = Str(d, "label");
                t.Mode = CleanTarget.ParseMode(Str(d, "mode"));
                t.Risk = ParseRisk(Str(d, "risk"));
                t.Admin = Bool(d, "admin");
                t.Recommend = Bool(d, "recommend");
                t.Deletable = Bool(d, "deletable");
                t.Bytes = Long(d, "bytes");
                t.Files = Long(d, "files");
                t.Denied = Bool(d, "denied");
                t.Note = Str(d, "note");
                t.Paths = StrList(d, "paths");
                t.Exists = true;
                list.Add(t);
            }
            return list;
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null) return Convert.ToString(v);
            return "";
        }

        private static bool Bool(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToBoolean(v); } catch { return false; }
            }
            return false;
        }

        private static long Long(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToInt64(v); } catch { return 0; }
            }
            return 0;
        }

        private static List<string> StrList(Dictionary<string, object> d, string key)
        {
            List<string> list = new List<string>();
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return list;

            IList arr = v as IList;
            if (arr != null)
            {
                foreach (object o in arr)
                    if (o != null) list.Add(Convert.ToString(o));
                return list;
            }
            string single = v as string;
            if (single != null && single.Length > 0) list.Add(single);
            return list;
        }

        private static RiskLevel ParseRisk(string s)
        {
            switch (s)
            {
                case "安全": return RiskLevel.Safe;
                case "低": return RiskLevel.Low;
                case "中": return RiskLevel.Medium;
                case "高": return RiskLevel.High;
                default: return RiskLevel.Safe;
            }
        }
    }
}
