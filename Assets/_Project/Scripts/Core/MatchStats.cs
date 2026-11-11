using System.Collections.Generic;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Per-player counters recorded during a match, written out by MatchResults for balance analysis.
    /// Unit types are keyed by <see cref="UnitKey"/> (display name without spaces, e.g. "BlockTrooper").
    /// </summary>
    public class MatchStats
    {
        public readonly Dictionary<string, int> UnitsBuilt = new Dictionary<string, int>();
        public readonly Dictionary<string, int> UnitsLost = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Kills = new Dictionary<string, int>();
        public readonly Dictionary<string, float> DamageDealt = new Dictionary<string, float>();
        public readonly List<string> Upgrades = new List<string>();
        public readonly List<int> PointsHeldSamples = new List<int>();

        public float FormEarned { get; set; }
        public float FormSpent { get; set; }

        public static string UnitKey(string displayName) => displayName.Replace(" ", "");

        public void Add(Dictionary<string, int> table, string key, int amount = 1)
        {
            table.TryGetValue(key, out int current);
            table[key] = current + amount;
        }

        public void AddDamage(string key, float amount)
        {
            DamageDealt.TryGetValue(key, out float current);
            DamageDealt[key] = current + amount;
        }
    }
}
