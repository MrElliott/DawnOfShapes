using UnityEngine;

namespace DawnOfShapes.Factions
{
    public enum UpgradeType
    {
        /// <summary>Flat armour added to every unit.</summary>
        Armour,
        /// <summary>Fractional damage bonus, 0.2 = +20%.</summary>
        Damage,
        /// <summary>Fractional move speed bonus.</summary>
        Speed,
        /// <summary>Fractional reduction in unit build time.</summary>
        BuildTime,
        /// <summary>Fractional reduction in unit Form cost.</summary>
        UnitCost
    }

    [CreateAssetMenu(menuName = "Dawn of Shapes/Upgrade Definition", fileName = "NewUpgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string displayName = "Upgrade";
        [TextArea] public string description;
        public UpgradeType type;
        public float amount = 0.2f;
        public int cost = 100;
        public float researchTime = 25f;
    }
}
