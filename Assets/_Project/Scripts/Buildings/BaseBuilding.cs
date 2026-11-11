using System.Collections.Generic;
using DawnOfShapes.Core;
using DawnOfShapes.Factions;
using DawnOfShapes.Units;
using UnityEngine;

namespace DawnOfShapes.Buildings
{
    /// <summary>
    /// A player's headquarters and unit factory. Losing it eliminates the player.
    /// </summary>
    public class BaseBuilding : Entity
    {
        private readonly List<UnitDefinition> queue = new List<UnitDefinition>();
        private PlayerState state;
        private MatchSettings settings;
        private float progress;

        public IReadOnlyList<UnitDefinition> Queue => queue;
        public bool IsBlockedByCap { get; private set; }
        public float Progress01 => queue.Count > 0 ? Mathf.Clamp01(progress / BuildTimeOf(queue[0])) : 0f;
        public Vector3 RallyPoint { get; set; }

        public override string DisplayName => state != null ? $"{state.Faction.FactionName} Base" : "Base";
        public override float Armour => settings != null ? settings.baseArmour : 0f;

        public void Initialise(PlayerState owner, MatchSettings matchSettings)
        {
            state = owner;
            settings = matchSettings;
            InitEntity(owner.PlayerId, settings.baseMaxHealth, Mathf.Max(settings.baseSize.x, settings.baseSize.z) * 0.5f);
            RallyPoint = transform.position + transform.forward * (Radius + 8f);
        }

        public int CostOf(UnitDefinition def) => Mathf.RoundToInt(def.cost * (1f - Mathf.Clamp01(state.GetBonus(UpgradeType.UnitCost))));
        public float BuildTimeOf(UnitDefinition def) => def.buildTime * (1f - Mathf.Clamp(state.GetBonus(UpgradeType.BuildTime), 0f, 0.8f));

        public bool CanQueue(UnitDefinition def)
        {
            return def != null && def.producible && queue.Count < settings.maxQueueLength && state.Form >= CostOf(def);
        }

        public bool TryQueue(UnitDefinition def)
        {
            if (!CanQueue(def)) return false;
            state.Form -= CostOf(def);
            queue.Add(def);
            return true;
        }

        private void Update()
        {
            if (queue.Count == 0 || IsDead) return;
            var match = MatchManager.Instance;
            if (match == null || match.Phase != MatchPhase.Playing) return;

            var def = queue[0];
            IsBlockedByCap = !match.HasPopRoom(OwnerPlayerId, def.popCost);
            if (IsBlockedByCap) return;

            progress += Time.deltaTime;
            if (progress < BuildTimeOf(def)) return;

            progress = 0f;
            queue.RemoveAt(0);
            Vector3 spawn = transform.position + transform.forward * (Radius + 1.5f) + transform.right * Random.Range(-2f, 2f);
            var unit = UnitFactory.Spawn(def, OwnerPlayerId, spawn, transform.rotation);
            unit.CommandMove(RallyPoint + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)));
        }
    }
}
