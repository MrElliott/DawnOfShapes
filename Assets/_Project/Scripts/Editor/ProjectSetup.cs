using System;
using System.Collections.Generic;
using System.IO;
using DawnOfShapes.Core;
using DawnOfShapes.Factions;
using DawnOfShapes.Map;
using DawnOfShapes.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnOfShapes.EditorTools
{
    /// <summary>
    /// Idempotent project bootstrap: URP, meshes, materials and the default game data assets.
    /// Assets are only created when missing, so values tuned in the inspector are kept.
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsFolder = "Assets/_Project/Settings";
        public const string DataFolder = "Assets/_Project/ScriptableObjects";
        public const string FactionsFolder = DataFolder + "/Factions";
        public const string UnitsFolder = DataFolder + "/Units";
        public const string UpgradesFolder = DataFolder + "/Upgrades";
        public const string MaterialsFolder = "Assets/_Project/Materials";
        public const string MeshesFolder = "Assets/_Project/Meshes";

        public const string LitShader = "Universal Render Pipeline/Lit";
        public const string UnlitShader = "Universal Render Pipeline/Unlit";

        public class GameData
        {
            public List<FactionDefinition> Factions;
            public MatchSettings Settings;
            public GameVisuals Visuals;
            public Mesh Box, Pyramid, Icosphere, Octahedron;
        }

        [MenuItem("Dawn of Shapes/Setup/Create Missing Game Data")]
        public static GameData EnsureAll()
        {
            EnsureUrp();
            foreach (var folder in new[] { FactionsFolder, UnitsFolder, UpgradesFolder, MaterialsFolder, MeshesFolder, SettingsFolder })
            {
                EnsureFolder(folder);
            }

            var data = new GameData
            {
                Box = EnsureMesh("Box", LowPolyMeshes.CreateBox),
                Pyramid = EnsureMesh("Pyramid", LowPolyMeshes.CreatePyramid),
                Icosphere = EnsureMesh("Icosphere", () => LowPolyMeshes.CreateIcosphere(1)),
                Octahedron = EnsureMesh("Octahedron", LowPolyMeshes.CreateOctahedron)
            };

            data.Settings = EnsureAsset<MatchSettings>(DataFolder + "/MatchSettings.asset", _ => { });
            EnsureCounterMatrix(data.Settings);
            data.Visuals = EnsureAsset<GameVisuals>(DataFolder + "/GameVisuals.asset", v => { });
            data.Visuals.unlitMaterial = EnsureMaterial("Unlit_Tint", Color.white, UnlitShader);
            data.Visuals.selectionMaterial = EnsureMaterial("SelectionRing", new Color(0.3f, 1f, 0.45f), LitShader);
            data.Visuals.boxMesh = data.Box;
            data.Visuals.projectileMesh = data.Icosphere;
            EditorUtility.SetDirty(data.Visuals);

            data.Factions = EnsureFactions(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>Creates the counter matrix asset if missing and links it from MatchSettings.</summary>
        public static void EnsureCounterMatrix(MatchSettings settings = null)
        {
            EnsureFolder(DataFolder);
            if (settings == null) settings = AssetDatabase.LoadAssetAtPath<MatchSettings>(DataFolder + "/MatchSettings.asset");
            var matrix = EnsureAsset<CounterMatrix>(DataFolder + "/CounterMatrix.asset",
                m => m.SetCycle(CounterMatrix.DefaultStrong, CounterMatrix.DefaultWeak));
            if (settings != null && settings.counters != matrix)
            {
                settings.counters = matrix;
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dawn of Shapes/Setup/Configure URP")]
        public static void EnsureUrp()
        {
            string assetPath = SettingsFolder + "/URP_PipelineAsset.asset";
            string rendererPath = SettingsFolder + "/URP_Renderer.asset";

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (pipeline == null)
            {
                EnsureFolder(SettingsFolder);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.shadowDistance = 150f;
                pipeline.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(pipeline, assetPath);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
        }

        private static List<FactionDefinition> EnsureFactions(GameData data)
        {
            // ---- Cubes: hold ground ----
            var trooper = EnsureUnit("Cubes_BlockTrooper", u =>
            {
                Set(u, "Block Trooper", "Melee tank. Soaks damage and holds points.", data.Box, new Vector3(1.4f, 1.4f, 1.4f));
                u.cost = 60; u.buildTime = 12f;
                u.maxHealth = 260f; u.armour = 30f; u.damage = 18f; u.range = 1.5f; u.attackCooldown = 1f;
                u.acquireRange = 10f; u.moveSpeed = 4f;
            });
            var stacker = EnsureUnit("Cubes_Stacker", u =>
            {
                Set(u, "Stacker", "Builder. Places cheap wall blocks [B]. Weak in a fight.", data.Box, new Vector3(1.2f, 0.8f, 1.2f));
                u.cost = 50; u.buildTime = 10f;
                u.maxHealth = 140f; u.armour = 15f; u.damage = 8f; u.range = 1.5f; u.attackCooldown = 1.2f;
                u.acquireRange = 6f; u.moveSpeed = 4.2f; u.canBuildWalls = true;
            });

            // ---- Spheres: move fast ----
            var miniRoller = EnsureUnit("Spheres_MiniRoller", u =>
            {
                Set(u, "Mini Roller", "Released when an Orb Cluster bursts.", data.Icosphere, new Vector3(0.7f, 0.7f, 0.7f));
                u.producible = false; u.popCost = 0; u.cost = 0;
                u.maxHealth = 45f; u.damage = 6f; u.range = 4f; u.attackCooldown = 0.7f;
                u.acquireRange = 12f; u.moveSpeed = 8.5f;
            });
            var roller = EnsureUnit("Spheres_Roller", u =>
            {
                Set(u, "Roller", "Fast, cheap skirmisher. Flanks and chases.", data.Icosphere, new Vector3(1.1f, 1.1f, 1.1f));
                u.cost = 35; u.buildTime = 7f;
                u.maxHealth = 90f; u.damage = 9f; u.range = 6f; u.attackCooldown = 0.7f;
                u.acquireRange = 14f; u.moveSpeed = 10f;
            });
            var orb = EnsureUnit("Spheres_OrbCluster", u =>
            {
                Set(u, "Orb Cluster", "Bursts into three Mini Rollers when destroyed.", data.Icosphere, new Vector3(1.7f, 1.7f, 1.7f));
                u.cost = 90; u.buildTime = 14f;
                u.maxHealth = 290f; u.damage = 12f; u.range = 5f; u.attackCooldown = 1f;
                u.acquireRange = 12f; u.moveSpeed = 6f;
                u.splitInto = miniRoller; u.splitCount = 3;
            });

            // ---- Pyramids: hit hard from range ----
            var spire = EnsureUnit("Pyramids_Spire", u =>
            {
                Set(u, "Spire", "Long-range artillery with splash damage. Fragile up close.", data.Pyramid, new Vector3(1.3f, 2.6f, 1.3f));
                u.cost = 110; u.buildTime = 16f;
                u.maxHealth = 130f; u.damage = 55f; u.range = 18f; u.attackCooldown = 2.6f;
                u.acquireRange = 20f; u.moveSpeed = 3.2f; u.projectileSpeed = 18f; u.splashRadius = 2f;
            });
            var scout = EnsureUnit("Pyramids_PrismScout", u =>
            {
                Set(u, "Prism Scout", "Fast spotter. Its beam slows enemies by 40%.", data.Octahedron, new Vector3(1f, 1.4f, 1f));
                u.cost = 60; u.buildTime = 9f;
                u.maxHealth = 120f; u.armour = 10f; u.damage = 10f; u.range = 9f; u.attackCooldown = 1f;
                u.acquireRange = 22f; u.moveSpeed = 7f; u.slowAmount = 0.4f; u.slowDuration = 2.5f;
            });

            var cubes = EnsureFaction("Cubes", ShapeType.Cube, new Color(0.22f, 0.47f, 0.92f),
                "Blocky, sturdy and orderly. Strong armour and walls; slow to chase.");
            var spheres = EnsureFaction("Spheres", ShapeType.Sphere, new Color(0.95f, 0.5f, 0.2f),
                "Fluid and mobile. Fast, cheap units that swarm and flank; fragile.");
            var pyramids = EnsureFaction("Pyramids", ShapeType.Pyramid, new Color(0.95f, 0.78f, 0.2f),
                "Sharp and precise. Long range and burst damage; expensive and weak up close.");

            FillFaction(cubes, data.Box, new List<UnitDefinition> { trooper, stacker },
                new List<UnitDefinition> { trooper, trooper, stacker }, "Foundry", 150, new List<UpgradeDefinition>
                {
                    EnsureUpgrade("Cubes_ReinforcedPlating", "Reinforced Plating", "+15% armour for all units.", UpgradeType.Armour, 15f, 120),
                    EnsureUpgrade("Cubes_HeavyBlocks", "Heavy Blocks", "+20% damage.", UpgradeType.Damage, 0.2f, 120),
                    EnsureUpgrade("Cubes_AssemblyLine", "Assembly Line", "Units build 25% faster.", UpgradeType.BuildTime, 0.25f, 100)
                });
            FillFaction(spheres, data.Icosphere, new List<UnitDefinition> { roller, orb },
                new List<UnitDefinition> { roller, roller, orb }, "Orbit Hub", 150, new List<UpgradeDefinition>
                {
                    EnsureUpgrade("Spheres_Momentum", "Momentum", "+20% move speed.", UpgradeType.Speed, 0.2f, 100),
                    EnsureUpgrade("Spheres_HardenedShells", "Hardened Shells", "+10% armour for all units.", UpgradeType.Armour, 10f, 110),
                    EnsureUpgrade("Spheres_MassProduction", "Mass Production", "Units cost 20% less.", UpgradeType.UnitCost, 0.2f, 120)
                });
            FillFaction(pyramids, data.Pyramid, new List<UnitDefinition> { spire, scout },
                new List<UnitDefinition> { scout, scout, spire }, "Observatory", 150, new List<UpgradeDefinition>
                {
                    EnsureUpgrade("Pyramids_FocusedLight", "Focused Light", "+25% damage.", UpgradeType.Damage, 0.25f, 140),
                    EnsureUpgrade("Pyramids_FacetedArmour", "Faceted Armour", "+10% armour for all units.", UpgradeType.Armour, 10f, 110),
                    EnsureUpgrade("Pyramids_Precision", "Precision Engineering", "Units build 25% faster.", UpgradeType.BuildTime, 0.25f, 100)
                });

            return new List<FactionDefinition> { cubes, spheres, pyramids };
        }

        // ---- Asset helpers ----

        private static void Set(UnitDefinition u, string name, string description, Mesh mesh, Vector3 scale)
        {
            u.displayName = name;
            u.description = description;
            u.mesh = mesh;
            u.scale = scale;
        }

        private static UnitDefinition EnsureUnit(string file, Action<UnitDefinition> init)
        {
            return EnsureAsset($"{UnitsFolder}/{file}.asset", init);
        }

        private static UpgradeDefinition EnsureUpgrade(string file, string name, string description, UpgradeType type, float amount, int cost)
        {
            return EnsureAsset<UpgradeDefinition>($"{UpgradesFolder}/{file}.asset", u =>
            {
                u.displayName = name;
                u.description = description;
                u.type = type;
                u.amount = amount;
                u.cost = cost;
                u.researchTime = 25f;
            });
        }

        private static FactionDefinition EnsureFaction(string name, ShapeType shape, Color color, string description)
        {
            return EnsureAsset<FactionDefinition>($"{FactionsFolder}/{name}.asset",
                f => f.EditorInitialise(name, shape, color, description));
        }

        private static void FillFaction(FactionDefinition faction, Mesh mesh, List<UnitDefinition> units, List<UnitDefinition> starting,
            string researchName, int researchCost, List<UpgradeDefinition> upgrades)
        {
            if (!faction.EditorNeedsContent) return;
            var material = EnsureMaterial("Faction_" + faction.FactionName, faction.Color, LitShader);
            faction.EditorSetContent(material, mesh, units, starting, researchName, researchCost, upgrades);
            EditorUtility.SetDirty(faction);
        }

        private static T EnsureAsset<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static Mesh EnsureMesh(string name, Func<Mesh> create)
        {
            string path = $"{MeshesFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = create();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Material EnsureMaterial(string name, Color color, string shaderName)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{shaderName}' not found. Is URP installed?");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
