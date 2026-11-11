using DawnOfShapes.Core;
using UnityEngine;

namespace DawnOfShapes.Units
{
    /// <summary>
    /// Short-lived line showing an instant ranged hit.
    /// </summary>
    public class Tracer : MonoBehaviour
    {
        private const float Lifetime = 0.08f;
        private float age;

        public static void Show(Vector3 from, Vector3 to, Color color)
        {
            var visuals = MatchManager.Instance != null ? MatchManager.Instance.Visuals : null;
            if (visuals == null || visuals.unlitMaterial == null) return;

            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = visuals.unlitMaterial;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.12f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Color.Lerp(color, Color.white, 0.4f));
            line.SetPropertyBlock(block);
            go.AddComponent<Tracer>();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
