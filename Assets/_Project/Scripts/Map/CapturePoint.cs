using UnityEngine;

namespace DawnOfShapes.Map
{
    /// <summary>
    /// Placeholder strategic point. Ownership and capture logic come later; territory morphing will key off these.
    /// </summary>
    public class CapturePoint : MonoBehaviour
    {
        public const int Unowned = -1;

        [SerializeField] private int pointId;
        [SerializeField] private float radius = 8f;
        [SerializeField] private int ownerPlayerId = Unowned;

        public int PointId => pointId;
        public float Radius => radius;
        public int OwnerPlayerId => ownerPlayerId;

        public void Initialise(int id, float captureRadius)
        {
            pointId = id;
            radius = captureRadius;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
