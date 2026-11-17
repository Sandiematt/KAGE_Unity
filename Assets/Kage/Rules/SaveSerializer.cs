using System;
using Newtonsoft.Json;

namespace Kage.Rules
{
    // SaveData to JSON text and back (ADR-0009). Pure: no file access (SaveStore in Kage.Runtime owns the file).
    public static class SaveSerializer
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            // a loaded save starts from an empty SaveData, so the file's dictionaries replace the defaults
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(SaveData save)
        {
            return JsonConvert.SerializeObject(save, Settings);
        }

        /// <summary>
        /// Reads a save from JSON text and fills it up to the current schema (GameRules.NormalizeSave).
        /// Returns false, with a message in <paramref name="error"/>, when the text is not a save at all.
        /// </summary>
        public static bool TryDeserialize(string json, out SaveData save, out string error)
        {
            save = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "empty save text";
                return false;
            }
            try
            {
                save = JsonConvert.DeserializeObject<SaveData>(json, Settings);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            if (save == null)
            {
                error = "save text is not an object";
                return false;
            }
            GameRules.NormalizeSave(save);
            return true;
        }
    }
}
