using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace USBHelperLauncher.Configuration
{
    public abstract class SettingsBase<T> where T : SettingsBase<T>
    {
        private static Lazy<List<KeyValuePair<PropertyInfo, Setting>>> _properties = new Lazy<List<KeyValuePair<PropertyInfo, Setting>>>(() =>
        {
            var doNotModify = typeof(SettingsBase<T>).GetProperty("DoNotModify", BindingFlags.NonPublic | BindingFlags.Static);
            return typeof(T)
                .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Concat(new[] { doNotModify })
                .Select(x => new KeyValuePair<PropertyInfo, Setting>(x, Setting.From(x)))
                .Where(x => x.Value != null)
                .ToList();
        });
        private static List<KeyValuePair<PropertyInfo, Setting>> Properties => _properties.Value;

        private static Lazy<string> _filePath = new Lazy<string>(() =>
            (string)typeof(T)
                .GetField("FilePath", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null)
        );

        private static string FilePath => _filePath.Value;


        [Setting("Launcher")]
        private static string DoNotModify { get; set; }


        public static void Save()
        {
            DoNotModify = Program.GetVersion();
            JObject conf = new JObject();
            foreach (var prop in Properties)
            {
                var section = prop.Value.Section;
                if (conf[section] == null)
                {
                    conf[section] = new JObject();
                }
                var obj = conf[section];
                var value = prop.Key.GetValue(null);
                if (value != null)
                {
                    obj[prop.Key.Name] = JToken.FromObject(value);
                }
            }
            File.WriteAllText(Path.Combine(Program.GetLauncherPath(), FilePath), conf.ToString());
        }

        public static void Load()
        {
            string path = Path.Combine(Program.GetLauncherPath(), FilePath);
            JObject conf;
            if (File.Exists(path))
            {
                conf = JObject.Parse(File.ReadAllText(path));
            }
            else
            {
                conf = new JObject();
            }
            foreach (var setting in Properties.OrderByDescending(x => x.Key.Name == nameof(DoNotModify)))
            {
                var forget = setting.Value.Forgetful && DoNotModify != Program.GetVersion();
                var token = conf.SelectToken(string.Join(".", setting.Value.Section, setting.Key.Name));
                var value = token == null || forget ? setting.Value.Default : token.ToObject(setting.Key.PropertyType);
                if (value != null)
                {
                    setting.Key.SetValue(null, value);
                }
            }
        }
    }
}
