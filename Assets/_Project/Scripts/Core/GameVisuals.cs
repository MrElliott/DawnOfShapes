using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Shared runtime art references (materials and meshes not owned by a faction).
    /// </summary>
    [CreateAssetMenu(menuName = "Dawn of Shapes/Game Visuals", fileName = "GameVisuals")]
    public class GameVisuals : ScriptableObject
    {
        [Tooltip("URP Unlit material, tinted per use with _BaseColor.")]
        public Material unlitMaterial;
        public Material selectionMaterial;
        public Mesh boxMesh;
        public Mesh projectileMesh;
        public Color neutralColor = new Color(0.75f, 0.75f, 0.72f);
    }
}
