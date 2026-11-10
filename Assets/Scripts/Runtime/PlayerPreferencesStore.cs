using System;
using System.IO;
using System.Text;
using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class PlayerPreferencesStore
    {
        private readonly string path;
        public PlayerPreferencesStore(string path) { this.path = path; }
        public PlayerPreferences Read(out bool invalid)
        {
            invalid = false;
            try
            {
                if (!File.Exists(path)) return new PlayerPreferences();
                if (new FileInfo(path).Length > 16384) throw new ArgumentException();
                string json = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}")) throw new ArgumentException();
                var result = new PlayerPreferences { Version = 0 };
                JsonUtility.FromJsonOverwrite(json, result);
                if (result.Version != 1) throw new ArgumentException();
                return result;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { invalid = true; return new PlayerPreferences(); }
        }

        public bool Write(PlayerPreferences settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(settings, true));
                    file.Write(bytes, 0, bytes.Length); file.Flush(true);
                }
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { return false; }
        }
    }
}
