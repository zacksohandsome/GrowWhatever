using System;
using System.IO;
using System.Text;
using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    public enum SaveReadStatus { Missing, Loaded, Invalid, Unsupported }

    /// <summary>Single-file persistence. A failed read never writes or deletes the original.</summary>
    public sealed class FarmSaveStore
    {
        private readonly string path;
        public FarmSaveStore(string path) { this.path = path; }

        public SaveReadStatus Read(out FarmSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                if (!File.Exists(path)) return SaveReadStatus.Missing;
                if (new FileInfo(path).Length > 2 * 1024 * 1024) return SaveReadStatus.Invalid;
                snapshot = JsonUtility.FromJson<FarmSnapshot>(File.ReadAllText(path, Encoding.UTF8));
                if (snapshot == null) return SaveReadStatus.Invalid;
                return snapshot.Version == FarmGame.SaveVersion ? SaveReadStatus.Loaded : SaveReadStatus.Unsupported;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { return SaveReadStatus.Invalid; }
        }

        public bool Write(FarmSnapshot snapshot)
        {
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(snapshot, true));
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { return false; }
        }
    }
}
