using DawnOfShapes.Core;

namespace DawnOfShapes.Buildings
{
    /// <summary>
    /// Cheap blocking wall placed by builder units. Carves the NavMesh so units path around it.
    /// </summary>
    public class Wall : Entity
    {
        private float armour;

        public override string DisplayName => "Wall Block";
        public override float Armour => armour;

        public void Initialise(int owner, float health, float wallArmour, float radius)
        {
            armour = wallArmour;
            InitEntity(owner, health, radius);
        }
    }
}
