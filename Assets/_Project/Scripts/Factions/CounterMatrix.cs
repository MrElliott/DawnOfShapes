using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DawnOfShapes.Factions
{
    /// <summary>
    /// Shape counter damage multipliers, indexed attacker shape x defender shape. Applied to unit-on-unit hits
    /// after percentage armour; buildings are unaffected. The intended cycle is Cube > Sphere > Pyramid > Cube.
    /// </summary>
    [CreateAssetMenu(menuName = "Dawn of Shapes/Counter Matrix", fileName = "CounterMatrix")]
    public class CounterMatrix : ScriptableObject
    {
        public const int ShapeCount = 3;
        public const float DefaultStrong = 1.2f;
        public const float DefaultWeak = 0.9f;
        private const float MinMultiplier = 0.1f, MaxMultiplier = 5f;

        [Tooltip("Row = attacker, column = defender, in Cube, Sphere, Pyramid order. 1 = no effect.")]
        [SerializeField] private float[] multipliers = Cycle(DefaultStrong, DefaultWeak);

        /// <summary>The shape that <paramref name="shape"/> is strong against.</summary>
        public static ShapeType Counters(ShapeType shape) => (ShapeType)(((int)shape + 1) % ShapeCount);

        /// <summary>The shape that is strong against <paramref name="shape"/>.</summary>
        public static ShapeType CounteredBy(ShapeType shape) => (ShapeType)(((int)shape + 2) % ShapeCount);

        public static string Plural(ShapeType shape) => shape + "s";

        /// <summary>Matrix where each shape deals <paramref name="strong"/> to the shape it counters and
        /// <paramref name="weak"/> to the shape that counters it.</summary>
        public static float[] Cycle(float strong, float weak)
        {
            var m = new float[ShapeCount * ShapeCount];
            for (int a = 0; a < ShapeCount; a++)
            {
                for (int d = 0; d < ShapeCount; d++)
                {
                    var att = (ShapeType)a;
                    var def = (ShapeType)d;
                    m[a * ShapeCount + d] = def == Counters(att) ? strong : def == CounteredBy(att) ? weak : 1f;
                }
            }
            return m;
        }

        public float Get(ShapeType attacker, ShapeType defender)
        {
            EnsureSize();
            return multipliers[(int)attacker * ShapeCount + (int)defender];
        }

        public void Set(ShapeType attacker, ShapeType defender, float value)
        {
            EnsureSize();
            multipliers[(int)attacker * ShapeCount + (int)defender] = Mathf.Clamp(value, MinMultiplier, MaxMultiplier);
        }

        public void SetCycle(float strong, float weak)
        {
            multipliers = Cycle(Mathf.Clamp(strong, MinMultiplier, MaxMultiplier), Mathf.Clamp(weak, MinMultiplier, MaxMultiplier));
        }

        /// <summary>Shapes this shape deals bonus damage to.</summary>
        public List<ShapeType> StrongAgainst(ShapeType shape)
        {
            var list = new List<ShapeType>();
            for (int d = 0; d < ShapeCount; d++)
            {
                if (Get(shape, (ShapeType)d) > 1f) list.Add((ShapeType)d);
            }
            return list;
        }

        /// <summary>Shapes this shape deals reduced damage to, or takes bonus damage from.</summary>
        public List<ShapeType> WeakAgainst(ShapeType shape)
        {
            var list = new List<ShapeType>();
            for (int o = 0; o < ShapeCount; o++)
            {
                var other = (ShapeType)o;
                if (Get(shape, other) < 1f || Get(other, shape) > 1f) list.Add(other);
            }
            return list;
        }

        /// <summary>Player-facing summary, e.g. "Strong vs Spheres · Weak vs Pyramids".</summary>
        public string Describe(ShapeType shape)
        {
            var strong = StrongAgainst(shape);
            var weak = WeakAgainst(shape);
            var parts = new List<string>();
            if (strong.Count > 0) parts.Add("Strong vs " + string.Join(", ", strong.ConvertAll(Plural)));
            if (weak.Count > 0) parts.Add("Weak vs " + string.Join(", ", weak.ConvertAll(Plural)));
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "No shape counters";
        }

        public Dictionary<string, Dictionary<string, float>> ToTable()
        {
            var table = new Dictionary<string, Dictionary<string, float>>();
            for (int a = 0; a < ShapeCount; a++)
            {
                var row = new Dictionary<string, float>();
                for (int d = 0; d < ShapeCount; d++) row[((ShapeType)d).ToString()] = Get((ShapeType)a, (ShapeType)d);
                table[((ShapeType)a).ToString()] = row;
            }
            return table;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("attacker -> defender:");
            for (int a = 0; a < ShapeCount; a++)
            {
                sb.Append(' ').Append((ShapeType)a).Append('[');
                for (int d = 0; d < ShapeCount; d++)
                {
                    if (d > 0) sb.Append(' ');
                    sb.Append(((ShapeType)d).ToString()[0]).Append('=').Append(Get((ShapeType)a, (ShapeType)d).ToString("0.00"));
                }
                sb.Append(']');
            }
            return sb.ToString();
        }

        private void EnsureSize()
        {
            if (multipliers == null || multipliers.Length != ShapeCount * ShapeCount) multipliers = Cycle(1f, 1f);
        }
    }
}
