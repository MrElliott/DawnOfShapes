using System.Collections.Generic;
using DawnOfShapes.Buildings;
using DawnOfShapes.Factions;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Runtime match state for one player: economy, score, upgrades and buildings.
    /// </summary>
    public class PlayerState
    {
        private readonly HashSet<UpgradeDefinition> upgrades = new HashSet<UpgradeDefinition>();

        public PlayerState(Player player, float startingForm, float startingVictoryPoints)
        {
            Player = player;
            Form = startingForm;
            VictoryPoints = startingVictoryPoints;
        }

        public Player Player { get; }
        public int PlayerId => Player.PlayerId;
        public int TeamId => Player.TeamId;
        public FactionDefinition Faction => Player.Faction;

        public float Form { get; set; }
        public float FormPerSecond { get; set; }
        public float VictoryPoints { get; set; }
        public bool Eliminated { get; set; }
        public string EliminationReason { get; set; }

        public MatchStats Stats { get; } = new MatchStats();

        /// <summary>Spends Form and records it in the stats. Returns false if there is not enough.</summary>
        public bool TrySpend(float amount)
        {
            if (Form < amount) return false;
            Form -= amount;
            Stats.FormSpent += amount;
            return true;
        }

        public BaseBuilding Base { get; set; }
        public ResearchBuilding Research { get; set; }

        public bool HasUpgrade(UpgradeDefinition upgrade) => upgrades.Contains(upgrade);
        public void AddUpgrade(UpgradeDefinition upgrade) => upgrades.Add(upgrade);

        public float GetBonus(UpgradeType type)
        {
            float total = 0f;
            foreach (var upgrade in upgrades)
            {
                if (upgrade.type == type) total += upgrade.amount;
            }
            return total;
        }
    }
}
