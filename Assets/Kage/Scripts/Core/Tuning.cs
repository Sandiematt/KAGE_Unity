using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Names the tuning file of a tuning class: <c>[TuningFile("combat")]</c> reads Resources/Tuning/combat.json.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TuningFileAttribute : Attribute
    {
        public readonly string Name;
        public TuningFileAttribute(string name) { Name = name; }
    }

    /// <summary>
    /// Numbers the owner changes without code (ADR-0001). Each feature has one JSON file under
    /// <c>Assets/Kage/Resources/Tuning/&lt;feature&gt;.json</c> and one plain <c>[Serializable]</c> class whose
    /// field initialisers are the defaults. A missing file, or a field the file leaves out, means the default.
    /// Values are read once and cached; tests and dev tools can override a class for a while.
    /// </summary>
    public static class Tuning
    {
        static readonly Dictionary<Type, object> Cache = new();
        static readonly Dictionary<Type, object> Overrides = new();

        public static T Get<T>() where T : class, new()
        {
            if (Overrides.TryGetValue(typeof(T), out var o)) return (T)o;
            if (Cache.TryGetValue(typeof(T), out var c)) return (T)c;
            var value = Load<T>();
            Cache[typeof(T)] = value;
            return value;
        }

        /// <summary>Uses <paramref name="value"/> instead of the file until <see cref="ClearOverrides"/>.</summary>
        public static void Override<T>(T value) where T : class, new() => Overrides[typeof(T)] = value;

        public static void ClearOverrides() => Overrides.Clear();

        /// <summary>Forgets the cached files, so the next Get reads them again.</summary>
        public static void Reload() => Cache.Clear();

        public static string FileOf(Type t)
        {
            var a = (TuningFileAttribute)Attribute.GetCustomAttribute(t, typeof(TuningFileAttribute));
            return a != null ? a.Name : t.Name;
        }

        static T Load<T>() where T : class, new()
        {
            var value = new T();
            var file = Resources.Load<TextAsset>("Tuning/" + FileOf(typeof(T)));
            if (file == null) return value;
            try { JsonUtility.FromJsonOverwrite(file.text, value); }
            catch (ArgumentException e) { Debug.LogWarning($"Tuning/{FileOf(typeof(T))}.json is not valid JSON, using defaults: {e.Message}"); }
            return value;
        }
    }
}
