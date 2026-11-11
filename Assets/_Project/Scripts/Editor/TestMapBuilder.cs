using System;
using System.Collections.Generic;
using System.IO;
using DawnOfShapes.CameraControl;
using DawnOfShapes.Core;
using DawnOfShapes.Factions;
using DawnOfShapes.Map;
using DawnOfShapes.Selection;
using DawnOfShapes.Units;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DawnOfShapes.EditorTools
{
    /// <summary>
    /// Generates the TestMap scene entirely from code so it can be rebuilt in batch mode.
    /// Usage: Unity -batchmode -quit -projectPath . -executeMethod DawnOfShapes.EditorTools.TestMapBuilder.BuildFromCommandLine
    /// </summary>
    public static class TestMapBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TestMap.unity";
        private const string SceneDataFolder = "Assets/_Project/Scenes/TestMap";
        private const string MaterialsFolder = "Assets/_Project/Materials";
        private const string MeshesFolder = "Assets/_Project/Meshes";

        private const float MapSize = 200f;
        private const int GroundCells = 40;
        private const float CaptureRadius = 8f;

        // Map layout data. Player count follows the number of spawn slots, never a fixed 2.
        private static readonly Vector3[] SpawnPositions =
        {
            new Vector3(-75f, 0f, -75f),
            new Vector3(75f, 0f, 75f),
        };

        private static readonly Vector3[] CapturePositions =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-50f, 0f, 40f),
            new Vector3(50f, 0f, -40f),
            new Vector3(40f, 0f, 60f),
            new Vector3(-40f, 0f, -60f),
        };

        private static readonly ShapeType[] StartingUnits = { ShapeType.Cube, ShapeType.Sphere, ShapeType.Pyramid };

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Dawn of Shapes/Build Test Map")]
        public static void Build()
        {
            ProjectSetup.EnsureUrp();
            List<FactionDefinition> factions = ProjectSetup.EnsureFactions();
            EnsureFolder(SceneDataFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Materials and meshes
            Material groundMat = SaveMaterial("Ground", new Color(0.42f, 0.55f, 0.36f));
            Material neutralMat = SaveMaterial("CapturePoint_Neutral", new Color(0.75f, 0.75f, 0.72f));
            Material pillarMat = SaveMaterial("CapturePoint_Pillar", new Color(0.95f, 0.95f, 0.92f));
            Material selectionMat = SaveMaterial("SelectionRing", new Color(0.3f, 1f, 0.45f));
            var factionMats = new Dictionary<FactionDefinition, Material>();
            foreach (var faction in factions)
            {
                factionMats[faction] = SaveMaterial("Faction_" + faction.FactionName, faction.Color);
            }

            Mesh groundMesh = SaveMesh(LowPolyMeshes.CreateGround(MapSize, GroundCells, 0.35f, 1234), "LowPolyGround");
            Mesh pyramidMesh = SaveMesh(LowPolyMeshes.CreatePyramid(), "Pyramid");

            // Lighting
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
            RenderSettings.sun = light;

            // Environment (everything under here is baked into the NavMesh)
            var environment = new GameObject("Environment");
            var ground = new GameObject("Ground");
            ground.transform.SetParent(environment.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = groundMat;
            ground.AddComponent<MeshCollider>().sharedMesh = groundMesh;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            var capturesRoot = new GameObject("CapturePoints");
            capturesRoot.transform.SetParent(environment.transform, false);
            for (int i = 0; i < CapturePositions.Length; i++)
            {
                CreateCapturePoint(i, CapturePositions[i], capturesRoot.transform, neutralMat, pillarMat);
            }

            var spawnsRoot = new GameObject("SpawnSlots");
            var slots = new List<SpawnSlot>();
            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                var slotGo = new GameObject($"SpawnSlot_{i}");
                slotGo.transform.SetParent(spawnsRoot.transform, false);
                slotGo.transform.position = SpawnPositions[i];
                var slot = slotGo.AddComponent<SpawnSlot>();
                slot.Initialise(i);
                slots.Add(slot);
            }

            BakeNavMesh(environment);

            // Players: one per spawn slot, free-for-all teams, factions assigned round-robin.
            var players = new List<Player>();
            for (int i = 0; i < slots.Count; i++)
            {
                var faction = factions[i % factions.Count];
                players.Add(new Player(i, i, $"Player {i + 1} ({faction.FactionName})", faction, slots[i].SlotIndex, i == 0));
            }

            var gameRoot = new GameObject("Game");
            var registry = gameRoot.AddComponent<PlayerRegistry>();
            registry.SetPlayers(players, 0);

            // Units
            var unitsRoot = new GameObject("Units");
            foreach (var player in players)
            {
                SpawnSlot slot = slots[player.SpawnSlot];
                Vector3 toCentre = (-slot.transform.position).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, toCentre);
                for (int u = 0; u < StartingUnits.Length; u++)
                {
                    Vector3 pos = slot.transform.position + toCentre * 4f + side * ((u - (StartingUnits.Length - 1) * 0.5f) * 3f);
                    CreateUnit(player, StartingUnits[u], pos, factionMats[player.Faction], selectionMat, pyramidMesh, unitsRoot.transform);
                }
            }

            // Camera rig, starting over the local player's spawn looking towards the centre.
            var rig = new GameObject("CameraRig");
            Vector3 localSpawn = slots[registry.LocalPlayer.SpawnSlot].transform.position;
            rig.transform.position = localSpawn;
            rig.transform.rotation = Quaternion.LookRotation(-localSpawn.normalized, Vector3.up);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.68f, 0.82f);
            cam.farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camGo.transform.localPosition = Quaternion.Euler(50f, 0f, 0f) * new Vector3(0f, 0f, -50f);
            camGo.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
            var rtsCam = rig.AddComponent<RTSCameraController>();
            float half = MapSize * 0.5f;
            rtsCam.Configure(cam, new Vector2(-half, -half), new Vector2(half, half));

            var selection = gameRoot.AddComponent<SelectionManager>();
            selection.Configure(cam);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DawnOfShapes] Built {ScenePath}: {players.Count} players, {CapturePositions.Length} capture points.");
        }

        private static void CreateCapturePoint(int id, Vector3 position, Transform parent, Material ringMat, Material pillarMat)
        {
            var root = new GameObject($"CapturePoint_{id}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.AddComponent<CapturePoint>().Initialise(id, CaptureRadius);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Marker";
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            ring.transform.localScale = new Vector3(CaptureRadius * 2f, 0.05f, CaptureRadius * 2f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 2f, 0f);
            pillar.transform.localScale = new Vector3(0.8f, 2f, 0.8f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
        }

        private static void CreateUnit(Player player, ShapeType shape, Vector3 position, Material mat, Material selectionMat, Mesh pyramidMesh, Transform parent)
        {
            var root = new GameObject($"P{player.PlayerId}_{shape}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            GameObject body;
            if (shape == ShapeType.Pyramid)
            {
                body = new GameObject("Body");
                body.AddComponent<MeshFilter>().sharedMesh = pyramidMesh;
                body.AddComponent<MeshRenderer>();
                var col = body.AddComponent<MeshCollider>();
                col.sharedMesh = pyramidMesh;
                col.convex = true;
            }
            else
            {
                body = GameObject.CreatePrimitive(shape == ShapeType.Cube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                body.name = "Body";
            }
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 1.4f;
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "SelectionIndicator";
            UnityEngine.Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.transform.SetParent(root.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            indicator.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
            var indicatorRenderer = indicator.GetComponent<MeshRenderer>();
            indicatorRenderer.sharedMaterial = selectionMat;
            indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            indicator.SetActive(false);

            var unit = root.AddComponent<Unit>();
            unit.Initialise(player.PlayerId, shape, indicator);

            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.8f;
            agent.height = 1.4f;
            agent.speed = shape == ShapeType.Sphere ? 8f : 6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.2f;
            agent.enabled = false; // enabled by Unit.Start once the NavMesh is loaded
        }

        private static void BakeNavMesh(GameObject environment)
        {
            var surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            string navPath = SceneDataFolder + "/NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        }

        private static Material SaveMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader not found. Is URP installed?");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshesFolder}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
