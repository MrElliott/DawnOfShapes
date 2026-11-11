using System.Collections.Generic;
using DawnOfShapes.Factions;
using UnityEngine;
using UnityEngine.AI;

namespace DawnOfShapes.Units
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class Unit : MonoBehaviour
    {
        private static readonly List<Unit> all = new List<Unit>();
        public static IReadOnlyList<Unit> All => all;

        [SerializeField] private int ownerPlayerId;
        [SerializeField] private ShapeType shape;
        [SerializeField] private GameObject selectionIndicator;

        private NavMeshAgent agent;

        public int OwnerPlayerId => ownerPlayerId;
        public ShapeType Shape => shape;
        public bool IsSelected { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            SetSelected(false);
        }

        private void Start()
        {
            // Agents are saved disabled and enabled here, after every NavMeshSurface.OnEnable has
            // registered its baked data, otherwise scene-load order can leave them off the NavMesh.
            if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            agent.enabled = true;
            if (!agent.isOnNavMesh) Debug.LogWarning($"{name} is not on the NavMesh.", this);
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public void Initialise(int owner, ShapeType unitShape, GameObject indicator)
        {
            ownerPlayerId = owner;
            shape = unitShape;
            selectionIndicator = indicator;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionIndicator != null) selectionIndicator.SetActive(selected);
        }

        public bool MoveTo(Vector3 destination)
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (NavMesh.SamplePosition(destination, out var hit, 4f, NavMesh.AllAreas))
            {
                return agent.SetDestination(hit.position);
            }
            return false;
        }
    }
}
