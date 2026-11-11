using UnityEngine;

namespace DawnOfShapes.Map
{
    /// <summary>
    /// A start location on the map. Players reference slots by index.
    /// </summary>
    public class SpawnSlot : MonoBehaviour
    {
        [SerializeField] private int slotIndex;

        public int SlotIndex => slotIndex;

        public void Initialise(int index) => slotIndex = index;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 6f);
        }
    }
}
