using DawnOfShapes.Core;
using DawnOfShapes.Factions;
using UnityEngine;

namespace DawnOfShapes.Buildings
{
    /// <summary>
    /// Researches the faction's upgrades one at a time. Upgrades already finished survive if it is destroyed.
    /// </summary>
    public class ResearchBuilding : Entity
    {
        private PlayerState state;
        private MatchSettings settings;
        private float progress;

        public UpgradeDefinition Current { get; private set; }
        public bool IsBusy => Current != null;
        public float Progress01 => Current != null ? Mathf.Clamp01(progress / Current.researchTime) : 0f;

        public override string DisplayName => state != null ? state.Faction.ResearchBuildingName : "Research";
        public override float Armour => settings != null ? settings.researchBuildingArmour : 0f;

        public void Initialise(PlayerState owner, MatchSettings matchSettings)
        {
            state = owner;
            settings = matchSettings;
            InitEntity(owner.PlayerId, settings.researchBuildingHealth,
                Mathf.Max(settings.researchBuildingSize.x, settings.researchBuildingSize.z) * 0.5f);
        }

        public bool CanResearch(UpgradeDefinition upgrade)
        {
            return upgrade != null && Current == null && !state.HasUpgrade(upgrade) && state.Form >= upgrade.cost;
        }

        public bool TryResearch(UpgradeDefinition upgrade)
        {
            if (!CanResearch(upgrade) || !state.TrySpend(upgrade.cost)) return false;
            Current = upgrade;
            progress = 0f;
            return true;
        }

        private void Update()
        {
            if (Current == null || IsDead) return;
            var match = MatchManager.Instance;
            if (match == null || match.Phase != MatchPhase.Playing) return;

            progress += Time.deltaTime;
            if (progress >= Current.researchTime)
            {
                state.AddUpgrade(Current);
                state.Stats.Upgrades.Add($"{Current.displayName}@{match.MatchTime:F0}s");
                Debug.Log($"[DawnOfShapes] {state.Player.DisplayName} researched {Current.displayName}.");
                Current = null;
            }
        }
    }
}
