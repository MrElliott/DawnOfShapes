using DawnOfShapes.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnOfShapes.Units
{
    /// <summary>
    /// Builds unit GameObjects at runtime from a UnitDefinition.
    /// </summary>
    public static class UnitFactory
    {
        public static Unit Spawn(UnitDefinition def, int ownerPlayerId, Vector3 position, Quaternion rotation)
        {
            var registry = PlayerRegistry.Instance;
            var player = registry != null ? registry.GetPlayer(ownerPlayerId) : null;
            var visuals = MatchManager.Instance != null ? MatchManager.Instance.Visuals : null;

            var root = new GameObject($"{def.displayName} (P{ownerPlayerId + 1})");
            root.transform.SetPositionAndRotation(Unit.SampleNavMesh(position), rotation);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = def.scale;
            body.transform.localPosition = new Vector3(0f, def.scale.y * 0.5f, 0f);
            body.AddComponent<MeshFilter>().sharedMesh = def.mesh;
            var renderer = body.AddComponent<MeshRenderer>();
            if (player != null && player.Faction != null) renderer.sharedMaterial = player.Faction.Material;
            body.AddComponent<BoxCollider>();

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "SelectionIndicator";
            Object.Destroy(indicator.GetComponent<Collider>());
            indicator.transform.SetParent(root.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            float ring = def.Radius * 2f + 0.6f;
            indicator.transform.localScale = new Vector3(ring, 0.02f, ring);
            var indicatorRenderer = indicator.GetComponent<MeshRenderer>();
            if (visuals != null) indicatorRenderer.sharedMaterial = visuals.selectionMaterial;
            indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            indicator.SetActive(false);

            var unit = root.AddComponent<Unit>();
            unit.Initialise(def, ownerPlayerId, indicator);
            return unit;
        }
    }
}
