using DawnOfShapes.Factions;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Match rules and economy tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "Dawn of Shapes/Match Settings", fileName = "MatchSettings")]
    public class MatchSettings : ScriptableObject
    {
        [Header("Capture points")]
        public float captureTime = 30f;

        [Header("Economy")]
        public float startingForm = 150f;
        public float formPerPointPerSecond = 1f;
        public float baseFormPerSecond = 1.5f;
        public int baseUnitCap = 12;
        public int unitCapPerPoint = 2;
        public int maxQueueLength = 5;

        [Header("Win conditions")]
        [Tooltip("Seconds one team must hold every capture point to win.")]
        public float dominationHoldTime = 300f;
        public float startingVictoryPoints = 1000f;
        [Tooltip("Victory points drained from each enemy player per second, per capture point held.")]
        public float victoryPointDrainPerPoint = 0.25f;

        [Header("Buildings")]
        public float baseMaxHealth = 20000f;
        [Tooltip("Armour values are percentage damage reduction.")]
        public float baseArmour = 60f;
        public Vector3 baseSize = new Vector3(7f, 5f, 7f);
        public float researchBuildingHealth = 600f;
        public float researchBuildingArmour = 25f;
        public Vector3 researchBuildingSize = new Vector3(4f, 5f, 4f);

        [Header("Walls")]
        public int wallCost = 25;
        public float wallHealth = 300f;
        public float wallArmour = 50f;
        public Vector3 wallSize = new Vector3(4f, 1.8f, 1f);

        [Header("Counters")]
        [Tooltip("Shape counter damage multipliers. A default cycle is used when empty.")]
        public CounterMatrix counters;
    }
}
