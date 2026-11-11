using DawnOfShapes.Core;
using UnityEngine;
using UnityEngine.AI;

namespace DawnOfShapes.Buildings
{
    /// <summary>
    /// Creates building GameObjects at runtime. Buildings carve the NavMesh.
    /// </summary>
    public static class BuildingFactory
    {
        public static BaseBuilding SpawnBase(PlayerState owner, MatchSettings settings, Vector3 position, Quaternion rotation)
        {
            var go = CreateShell($"{owner.Faction.FactionName} Base (P{owner.PlayerId + 1})", owner.Faction.ShapeMesh,
                owner.Faction.Material, settings.baseSize, position, rotation);
            var building = go.AddComponent<BaseBuilding>();
            building.Initialise(owner, settings);
            return building;
        }

        public static ResearchBuilding SpawnResearch(PlayerState owner, MatchSettings settings, Vector3 position, Quaternion rotation)
        {
            var go = CreateShell($"{owner.Faction.ResearchBuildingName} (P{owner.PlayerId + 1})", owner.Faction.ShapeMesh,
                owner.Faction.Material, settings.researchBuildingSize, position, rotation);
            // A small cap block marks it apart from the base.
            var visuals = MatchManager.Instance != null ? MatchManager.Instance.Visuals : null;
            if (visuals != null)
            {
                var cap = new GameObject("Cap");
                cap.transform.SetParent(go.transform, false);
                cap.transform.localPosition = new Vector3(0f, settings.researchBuildingSize.y + 0.6f, 0f);
                cap.transform.localScale = Vector3.one * 1.2f;
                cap.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                cap.AddComponent<MeshFilter>().sharedMesh = visuals.boxMesh;
                cap.AddComponent<MeshRenderer>().sharedMaterial = visuals.selectionMaterial;
            }
            var building = go.AddComponent<ResearchBuilding>();
            building.Initialise(owner, settings);
            return building;
        }

        public static Wall SpawnWall(PlayerState owner, MatchSettings settings, Mesh mesh, Vector3 position, Quaternion rotation)
        {
            var go = CreateShell($"Wall (P{owner.PlayerId + 1})", mesh, owner.Faction.Material, settings.wallSize, position, rotation);
            var wall = go.AddComponent<Wall>();
            wall.Initialise(owner.PlayerId, settings.wallHealth, settings.wallArmour, settings.wallSize.z * 0.5f + 0.5f);
            return wall;
        }

        private static GameObject CreateShell(string name, Mesh mesh, Material material, Vector3 size, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(name);
            if (NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas)) position = hit.position;
            root.transform.SetPositionAndRotation(position, rotation);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            body.transform.localScale = size;
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = material;
            body.AddComponent<BoxCollider>();

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, size.y * 0.5f, 0f);
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            return root;
        }
    }
}
