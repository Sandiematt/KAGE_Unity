using System;
using System.IO;
using System.Text;
using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Owns the save file (ADR-0009): <c>kage_save.json</c> in one folder. It never throws: a missing, empty or corrupt
    /// file gives a fresh save, and a failed write is logged and reported by the return value.
    /// </summary>
    public sealed class SaveStore
    {
        public const string FileName = "kage_save.json";

        readonly string path;

        /// <summary>A store over <paramref name="directory"/>. Tests pass a temporary folder.</summary>
        public SaveStore(string directory)
        {
            path = Path.Combine(directory, FileName);
        }

        /// <summary>The store the game uses: <c>Application.persistentDataPath/kage_save.json</c>.</summary>
        public static SaveStore AtPersistentDataPath()
        {
            return new SaveStore(Application.persistentDataPath);
        }

        /// <summary>The full path of the save file.</summary>
        public string FilePath { get { return path; } }

        string TempPath { get { return path + ".tmp"; } }

        string CorruptPath { get { return path + ".corrupt"; } }

        /// <summary>
        /// Reads the save. A missing file gives a fresh save, written at once. A file that doesn't parse is copied to
        /// <c>kage_save.json.corrupt</c> and replaced by a fresh save, with a warning. A dev-mode save gets the dev grants.
        /// </summary>
        public SaveData Load()
        {
            string text = null;
            try
            {
                if (File.Exists(path)) text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                // The file is there but can't be read now (locked, no permission). Play on a fresh save in memory and
                // leave the file alone, so a later session can still read it.
                Debug.LogWarning("Kage: could not read " + path + ": " + e.Message);
                return GameRules.NewInstallSave();
            }

            var opened = GameRules.OpenSave(text);
            if (opened.CorruptText != null)
            {
                Debug.LogWarning("Kage: the save file is not readable (" + opened.Error + "). Starting a fresh save; the old text is kept in " + CorruptPath);
                try { File.WriteAllText(CorruptPath, opened.CorruptText, new UTF8Encoding(false)); }
                catch (Exception e) { Debug.LogWarning("Kage: could not keep the unreadable save: " + e.Message); }
            }
            if (opened.Fresh || opened.CorruptText != null) Save(opened.Save);
            return opened.Save;
        }

        /// <summary>
        /// Writes the save: to <c>kage_save.json.tmp</c> first, then over the real file, so a crash mid-write never leaves
        /// half a save. Returns false (and logs a warning) if the file could not be written.
        /// </summary>
        public bool Save(SaveData save)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(TempPath, SaveSerializer.Serialize(save), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(TempPath, path, null);
                else File.Move(TempPath, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Kage: could not write " + path + ": " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Switches dev mode (GameRules.SetDevMode) and writes the result. Turning it off replaces the save with a fresh
        /// one, so callers confirm with the player first. Returns the save to play with.
        /// </summary>
        public SaveData SetDevMode(SaveData save, bool on)
        {
            var result = GameRules.SetDevMode(save, on);
            Save(result);
            return result;
        }
    }
}
