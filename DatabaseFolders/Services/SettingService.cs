using DatabaseFolders.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseFolders.Services
{
    public class SettingService
    {
        public static List<DbFolderSetting> GetSettings()
        {
            var settings = new List<DbFolderSetting>();
            var path = BuildJsonFilePath();

            if (!File.Exists(path))
            {
                SaveJsonFileToDisk(settings);

                return settings;
            }

            var jsonStr = File.ReadAllText(path);
            var json = JObject.Parse(jsonStr);

            if (json["folders"] != null)
            {
                var folders = (JObject)json["folders"];
                foreach (JProperty folder in folders.Properties())
                {
                    var data = new List<string>();
                    if (folder.Value is JArray array)
                    {
                        data = array.Values<string>().ToList();
                    }

                    settings.Add(new DbFolderSetting() { FolderName = folder.Name, Patterns = data.ToArray() });
                }
            }

            return settings;
        }

        public static bool DeleteSetting(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var settings = GetSettings();
                var currentSetting = settings.FirstOrDefault(s => s.FolderName.Equals(name));
                if (currentSetting != null)
                {
                    settings.Remove(currentSetting);

                    SaveJsonFileToDisk(settings);

                    return true;
                }
            }

            return false;
        }

        public static void AddSetting(DbFolderSetting model)
        {
            var settings = GetSettings();
            settings.Add(model);
            //settings = settings.OrderBy(s => s.FolderName).ToList();

            SaveJsonFileToDisk(settings);
        }


        public static bool EditSetting(string name, DbFolderSetting model)
        {
            var settings = GetSettings();
            var currentSetting = settings.FirstOrDefault(s => s.FolderName.Equals(name));
            if (currentSetting != null)
            {
                currentSetting.FolderName = model.FolderName;
                currentSetting.Patterns = model.Patterns;

                //settings = settings.OrderBy(s => s.FolderName).ToList();

                SaveJsonFileToDisk(settings);

                return true;
            }

            return false;
        }

        public static void MoveUp(string name)
        {
            var settings = GetSettings();
            var currentSetting = settings.FirstOrDefault(s => s.FolderName.Equals(name));
            if (currentSetting != null)
            {
                var oldIndex = settings.IndexOf(currentSetting);
                if (oldIndex == 0)
                    return;

                settings.RemoveAt(oldIndex);
                settings.Insert(oldIndex - 1, currentSetting);

                SaveJsonFileToDisk(settings);
            }
        }

        public static void MoveDown(string name)
        {
            var settings = GetSettings();
            var currentSetting = settings.FirstOrDefault(s => s.FolderName.Equals(name));
            if (currentSetting != null)
            {
                var oldIndex = settings.IndexOf(currentSetting);
                if (oldIndex == settings.Count - 1)
                    return;

                settings.RemoveAt(oldIndex);
                settings.Insert(oldIndex + 1, currentSetting);

                SaveJsonFileToDisk(settings);
            }
        }


        private static void SaveJsonFileToDisk(List<DbFolderSetting> settings)
        {
            var path = BuildJsonFilePath();
            var jFolders = new JObject();
            settings.ForEach(s =>
            {
                jFolders.Add(new JProperty(s.FolderName, new JArray(s.Patterns ?? new string[0])));
            });

            var jObj = new JObject(new JProperty("folders", jFolders));
            File.WriteAllText(path, jObj.ToString(Formatting.Indented));
        }

        private static string BuildJsonFilePath()
        {
            var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(location), "Settings.json");
        }
    }
}
