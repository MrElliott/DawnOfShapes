using System.Collections.Generic;
using DawnOfShapes.Units;
using UnityEngine;

namespace DawnOfShapes.Factions
{
    public enum ShapeType
    {
        Cube,
        Sphere,
        Pyramid
    }

    /// <summary>
    /// Data for one playable faction. Territory a faction controls morphs towards its shape style.
    /// </summary>
    [CreateAssetMenu(menuName = "Dawn of Shapes/Faction Definition", fileName = "NewFaction")]
    public class FactionDefinition : ScriptableObject
    {
        [SerializeField] private string factionName = "New Faction";
        [SerializeField] private ShapeType shape = ShapeType.Cube;
        [SerializeField] private Color color = Color.white;
        [SerializeField, TextArea] private string description;

        [Header("Visuals")]
        [SerializeField] private Material material;
        [Tooltip("Mesh used for this faction's buildings.")]
        [SerializeField] private Mesh shapeMesh;

        [Header("Units")]
        [SerializeField] private List<UnitDefinition> units = new List<UnitDefinition>();
        [SerializeField] private List<UnitDefinition> startingUnits = new List<UnitDefinition>();

        [Header("Research")]
        [SerializeField] private string researchBuildingName = "Research Building";
        [SerializeField] private int researchBuildingCost = 150;
        [SerializeField] private List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

        public string FactionName => factionName;
        public ShapeType Shape => shape;
        public Color Color => color;
        public string Description => description;
        public Material Material => material;
        public Mesh ShapeMesh => shapeMesh;
        public IReadOnlyList<UnitDefinition> Units => units;
        public IReadOnlyList<UnitDefinition> StartingUnits => startingUnits;
        public string ResearchBuildingName => researchBuildingName;
        public int ResearchBuildingCost => researchBuildingCost;
        public IReadOnlyList<UpgradeDefinition> Upgrades => upgrades;

#if UNITY_EDITOR
        public void EditorInitialise(string name, ShapeType shapeType, Color factionColor, string desc)
        {
            factionName = name;
            shape = shapeType;
            color = factionColor;
            description = desc;
        }

        public void EditorSetContent(Material mat, Mesh mesh, List<UnitDefinition> unitList, List<UnitDefinition> starting,
            string researchName, int researchCost, List<UpgradeDefinition> upgradeList)
        {
            material = mat;
            shapeMesh = mesh;
            units = unitList;
            startingUnits = starting;
            researchBuildingName = researchName;
            researchBuildingCost = researchCost;
            upgrades = upgradeList;
        }

        public bool EditorNeedsContent => material == null || shapeMesh == null || units.Count == 0 || upgrades.Count == 0;
#endif
    }
}
