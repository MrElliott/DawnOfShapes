using System;
using System.Collections.Generic;
using DawnOfShapes.CameraControl;
using DawnOfShapes.Core;
using DawnOfShapes.Map;
using DawnOfShapes.Selection;
using DawnOfShapes.UI;
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
    /// Bases and units are spawned at runtime by MatchManager from the spawn slots.
    /// Usage: Unity -batchmode -quit -projectPath . -executeMethod DawnOfShapes.EditorTools.TestMapBuilder.BuildFromCommandLine
    /// </summary>
    public static class TestMapBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TestMap.unity";
        private const string SceneDataFolder = "Assets/_Project/Scenes/TestMap";

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
            // New scene first: opening it unloads unreferenced assets, which would null out anything loaded before.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var data = ProjectSetup.EnsureAll();
            ProjectSetup.EnsureFolder(SceneDataFolder);

            Material groundMat = ProjectSetup.EnsureMaterial("Ground", new Color(0.42f, 0.55f, 0.36f), ProjectSetup.LitShader);
            Material markerMat = ProjectSetup.EnsureMaterial("CapturePoint_Neutral", new Color(0.75f, 0.75f, 0.72f), ProjectSetup.LitShader);
            Material pillarMat = ProjectSetup.EnsureMaterial("CapturePoint_Pillar", new Color(0.95f, 0.95f, 0.92f), ProjectSetup.LitShader);
            Mesh groundMesh = SaveMesh(LowPolyMeshes.CreateGround(MapSize, GroundCells, 0.35f, 1234), "LowPolyGround");

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
                CreateCapturePoint(i, CapturePositions[i], capturesRoot.transform, markerMat, pillarMat);
            }

            var spawnsRoot = new GameObject("SpawnSlots");
            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                var slotGo = new GameObject($"SpawnSlot_{i}");
                slotGo.transform.SetParent(spawnsRoot.transform, false);
                slotGo.transform.position = SpawnPositions[i];
                slotGo.AddComponent<SpawnSlot>().Initialise(i);
            }

            BakeNavMesh(environment);

            // Camera rig
            var rig = new GameObject("CameraRig");
            rig.transform.position = SpawnPositions[0];
            rig.transform.rotation = Quaternion.LookRotation(-SpawnPositions[0].normalized, Vector3.up);
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

            // Game systems
            var game = new GameObject("Game");
            game.AddComponent<PlayerRegistry>();
            var match = game.AddComponent<MatchManager>();
            var so = new SerializedObject(match);
            so.FindProperty("settings").objectReferenceValue = data.Settings;
            so.FindProperty("visuals").objectReferenceValue = data.Visuals;
            so.FindProperty("cameraRig").objectReferenceValue = rtsCam;
            var factionsProp = so.FindProperty("factions");
            factionsProp.arraySize = data.Factions.Count;
            for (int i = 0; i < data.Factions.Count; i++)
            {
                factionsProp.GetArrayElementAtIndex(i).objectReferenceValue = data.Factions[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var selection = game.AddComponent<SelectionManager>();
            selection.Configure(cam);
            game.AddComponent<HUD>().Configure(selection, cam);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DawnOfShapes] Built {ScenePath}: {SpawnPositions.Length} spawn slots, {CapturePositions.Length} capture points.");
        }

        private static void CreateCapturePoint(int id, Vector3 position, Transform parent, Material markerMat, Material pillarMat)
        {
            var root = new GameObject($"CapturePoint_{id}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            marker.transform.localScale = new Vector3(CaptureRadius * 2f, 0.05f, CaptureRadius * 2f);
            var markerRenderer = marker.GetComponent<MeshRenderer>();
            markerRenderer.sharedMaterial = markerMat;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 2f, 0f);
            pillar.transform.localScale = new Vector3(0.8f, 2f, 0.8f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;

            root.AddComponent<CapturePoint>().Initialise(id, CaptureRadius, markerRenderer);
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

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{ProjectSetup.MeshesFolder}/{name}.asset";
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
    }
}
