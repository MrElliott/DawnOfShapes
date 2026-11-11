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

        public string FactionName => factionName;
        public ShapeType Shape => shape;
        public Color Color => color;
        public string Description => description;

#if UNITY_EDITOR
        public void EditorInitialise(string name, ShapeType shapeType, Color factionColor, string desc)
        {
            factionName = name;
            shape = shapeType;
            color = factionColor;
            description = desc;
        }
#endif
    }
}
