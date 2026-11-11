using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using DawnOfShapes.Factions;
using DawnOfShapes.Units;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Applies a JSON file of stat overrides to the loaded data assets at runtime, so balance changes can be
    /// tested without a rebuild. Format (every section optional, keys are public field names):
    /// <code>
    /// {
    ///   "units":    { "Spire": { "damage": 55, "maxHealth": 130 } },
    ///   "upgrades": { "Momentum": { "amount": 0.25 } },
    ///   "factions": { "Cubes": { "researchBuildingCost": 120 } },
    ///   "settings": { "baseMaxHealth": 4000 }
    /// }
    /// </code>
    /// Units and upgrades are matched by display name with or without spaces, or by asset name.
    /// </summary>
    public static class BalanceOverrides
    {
        public static int Apply(string path, IReadOnlyList<FactionDefinition> factions, MatchSettings settings)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[DawnOfShapes] Balance file not found: {path}");
                return 0;
            }

            var root = JObject.Parse(File.ReadAllText(path));
            int applied = 0;

            if (root["units"] is JObject units)
            {
                var all = AllUnits(factions);
                foreach (var entry in units.Properties())
                {
                    var def = all.Find(u => Matches(entry.Name, u.displayName, u.name));
                    if (def == null) Warn($"unknown unit '{entry.Name}'");
                    else applied += ApplyFields(def, entry.Value as JObject, "unit " + entry.Name);
                }
            }

            if (root["upgrades"] is JObject upgrades)
            {
                var all = new List<UpgradeDefinition>();
                foreach (var f in factions) all.AddRange(f.Upgrades);
                foreach (var entry in upgrades.Properties())
                {
                    var up = all.Find(u => Matches(entry.Name, u.displayName, u.name));
                    if (up == null) Warn($"unknown upgrade '{entry.Name}'");
                    else applied += ApplyFields(up, entry.Value as JObject, "upgrade " + entry.Name);
                }
            }

            if (root["factions"] is JObject factionOverrides)
            {
                foreach (var entry in factionOverrides.Properties())
                {
                    FactionDefinition faction = null;
                    foreach (var f in factions)
                    {
                        if (Matches(entry.Name, f.FactionName, f.name)) faction = f;
                    }
                    if (faction == null) Warn($"unknown faction '{entry.Name}'");
                    else applied += ApplyFields(faction, entry.Value as JObject, "faction " + entry.Name);
                }
            }

            if (root["settings"] is JObject settingOverrides)
            {
                applied += ApplyFields(settings, settingOverrides, "settings");
            }

            Debug.Log($"[DawnOfShapes] Balance overrides from {path}: {applied} value(s) applied.");
            return applied;
        }

        public static List<UnitDefinition> AllUnits(IReadOnlyList<FactionDefinition> factions)
        {
            var list = new List<UnitDefinition>();
            foreach (var f in factions)
            {
                foreach (var u in f.Units)
                {
                    if (u != null && !list.Contains(u)) list.Add(u);
                    if (u != null && u.splitInto != null && !list.Contains(u.splitInto)) list.Add(u.splitInto);
                }
            }
            return list;
        }

        public static bool Matches(string key, string displayName, string assetName)
        {
            string k = key.Replace(" ", "");
            return string.Equals(k, displayName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(key, assetName, StringComparison.OrdinalIgnoreCase);
        }

        private static int ApplyFields(object target, JObject values, string label)
        {
            if (values == null) return 0;
            int count = 0;
            var type = target.GetType();
            foreach (var prop in values.Properties())
            {
                var field = type.GetField(prop.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                {
                    Warn($"{label}: no field '{prop.Name}'");
                    continue;
                }
                try
                {
                    object before = field.GetValue(target);
                    object raw = prop.Value is JValue jv ? jv.Value : prop.Value.ToString();
                    object value = Convert.ChangeType(raw, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(target, value);
                    Debug.Log($"[DawnOfShapes] Override {label}.{prop.Name}: {before} -> {value}");
                    count++;
                }
                catch (Exception e)
                {
                    Warn($"{label}.{prop.Name}: cannot set ({e.Message})");
                }
            }
            return count;
        }

        private static void Warn(string message) => Debug.LogWarning("[DawnOfShapes] Balance override skipped: " + message);
    }
}
