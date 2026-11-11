using System.Collections.Generic;
using DawnOfShapes.Factions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnOfShapes.EditorTools
{
    /// <summary>
    /// Idempotent project bootstrap: URP pipeline asset and the default faction definitions.
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsFolder = "Assets/_Project/Settings";
        public const string FactionsFolder = "Assets/_Project/ScriptableObjects/Factions";

        [MenuItem("Dawn of Shapes/Setup/Configure URP")]
        public static void EnsureUrp()
        {
            string assetPath = SettingsFolder + "/URP_PipelineAsset.asset";
            string rendererPath = SettingsFolder + "/URP_Renderer.asset";

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (pipeline == null)
            {
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
            Debug.Log("[DawnOfShapes] URP pipeline asset assigned.");
        }

        [MenuItem("Dawn of Shapes/Setup/Create Default Factions")]
        public static List<FactionDefinition> EnsureFactions()
        {
            return new List<FactionDefinition>
            {
                EnsureFaction("Cubes", ShapeType.Cube, new Color(0.22f, 0.47f, 0.92f),
                    "Blocky, sturdy and orderly. Territory becomes terraced and grid-aligned."),
                EnsureFaction("Spheres", ShapeType.Sphere, new Color(0.88f, 0.28f, 0.24f),
                    "Fluid and mobile. Territory rolls into soft, rounded hills."),
                EnsureFaction("Pyramids", ShapeType.Pyramid, new Color(0.95f, 0.76f, 0.2f),
                    "Sharp and aggressive. Territory juts into angular spikes and ridges."),
            };
        }

        private static FactionDefinition EnsureFaction(string name, ShapeType shape, Color color, string description)
        {
            string path = $"{FactionsFolder}/{name}.asset";
            var faction = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
            if (faction == null)
            {
                faction = ScriptableObject.CreateInstance<FactionDefinition>();
                faction.EditorInitialise(name, shape, color, description);
                AssetDatabase.CreateAsset(faction, path);
                AssetDatabase.SaveAssets();
            }
            return faction;
        }
    }
}
