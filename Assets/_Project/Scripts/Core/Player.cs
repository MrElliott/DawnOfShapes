using System;
using DawnOfShapes.Factions;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// One participant in a match. Players sharing a TeamId are allies.
    /// </summary>
    [Serializable]
    public class Player
    {
        [SerializeField] private int playerId;
        [SerializeField] private int teamId;
        [SerializeField] private string displayName;
        [SerializeField] private FactionDefinition faction;
        [SerializeField] private int spawnSlot;
        [SerializeField] private bool isHuman;

        public Player(int playerId, int teamId, string displayName, FactionDefinition faction, int spawnSlot, bool isHuman)
        {
            this.playerId = playerId;
            this.teamId = teamId;
            this.displayName = displayName;
            this.faction = faction;
            this.spawnSlot = spawnSlot;
            this.isHuman = isHuman;
        }

        public int PlayerId => playerId;
        public int TeamId => teamId;
        public string DisplayName => displayName;
        public FactionDefinition Faction => faction;
        public int SpawnSlot => spawnSlot;
        public bool IsHuman => isHuman;
        public Color Color => faction != null ? faction.Color : Color.grey;
    }
}
