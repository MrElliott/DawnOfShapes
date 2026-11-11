using System.Collections.Generic;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Scene-level list of every player in the match. Supports any number of players and teams.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerRegistry : MonoBehaviour
    {
        [SerializeField] private List<Player> players = new List<Player>();
        [SerializeField] private int localPlayerId;

        public static PlayerRegistry Instance { get; private set; }

        public IReadOnlyList<Player> Players => players;
        public int LocalPlayerId => localPlayerId;
        public Player LocalPlayer => GetPlayer(localPlayerId);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple PlayerRegistry instances in scene; ignoring the extra one.", this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Player GetPlayer(int playerId)
        {
            foreach (var player in players)
            {
                if (player.PlayerId == playerId) return player;
            }
            return null;
        }

        public bool AreAllies(int playerA, int playerB)
        {
            var a = GetPlayer(playerA);
            var b = GetPlayer(playerB);
            return a != null && b != null && a.TeamId == b.TeamId;
        }

        public IEnumerable<Player> GetTeam(int teamId)
        {
            foreach (var player in players)
            {
                if (player.TeamId == teamId) yield return player;
            }
        }

        public void SetPlayers(IEnumerable<Player> newPlayers, int localId)
        {
            players = new List<Player>(newPlayers);
            localPlayerId = localId;
        }
    }
}
