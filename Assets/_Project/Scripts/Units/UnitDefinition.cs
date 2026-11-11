using UnityEngine;

namespace DawnOfShapes.Units
{
    /// <summary>
    /// Stats and visuals for one unit type. Tune values in the inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Dawn of Shapes/Unit Definition", fileName = "NewUnit")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Unit";
        [TextArea] public string description;
        public Mesh mesh;
        public Vector3 scale = Vector3.one;

        [Header("Production")]
        public bool producible = true;
        public int cost = 50;
        public float buildTime = 10f;
        public int popCost = 1;

        [Header("Combat")]
        public float maxHealth = 100f;
        [Tooltip("Percentage damage reduction, 0 to 75.")]
        public float armour;
        public float damage = 10f;
        [Tooltip("Attack range measured edge to edge, in metres.")]
        public float range = 1.5f;
        public float attackCooldown = 1f;
        [Tooltip("Idle units engage enemies within this distance.")]
        public float acquireRange = 12f;
        [Tooltip("0 = instant hit. Above 0 fires a projectile at this speed (m/s).")]
        public float projectileSpeed;
        public float splashRadius;

        [Header("Movement")]
        public float moveSpeed = 5f;

        [Header("Special")]
        [Range(0f, 0.9f)] public float slowAmount;
        public float slowDuration;
        public bool canBuildWalls;
        public UnitDefinition splitInto;
        public int splitCount;

        public float Radius => Mathf.Max(scale.x, scale.z) * 0.5f;
    }
}
