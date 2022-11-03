using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class LobbyUI : MonoBehaviour
    {
        private Label _noneListLabel;

        private Label _redPlayersListLabel;

        private Label _bluePlayersListLabel;

        private Label _displayMessageLabel;

        private Label _readyStatusLabel;

        private void Start()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            
            _noneListLabel = root.Q<Label>("NoneList");
            _redPlayersListLabel = root.Q<Label>("RedPlayersList");
            _bluePlayersListLabel = root.Q<Label>("BluePlayersList");
            _displayMessageLabel = root.Q<Label>("DisplayMessage");
            _readyStatusLabel = root.Q<Label>("ReadyStatus");
        }

        private void Update()
        {
            PlayerController[] players = FindObjectsOfType<PlayerController>();

            _noneListLabel.text = BuildPlayerList(players, Team.None);
            _redPlayersListLabel.text = BuildPlayerList(players, Team.Red);
            _bluePlayersListLabel.text = BuildPlayerList(players, Team.Blue);

            _displayMessageLabel.text = GameManager.Instance.DisplayMessage;
            
            _readyStatusLabel.text = ReadyToStart();
        }

        private static string BuildPlayerList(IEnumerable<PlayerController> players, Team team)
        {
            return players.Where(p => p.Team == team).OrderBy(p => p.PlayerName).Aggregate(string.Empty, (current, player) => current + $"{player.PlayerName}\n");
        }

        private static string ReadyToStart()
        {
            string msg = GameManager.Instance.FriendlyFire ? "Friendly Fire On\n" : "Friendly Fire Off\n";
            msg += GameManager.Instance.LocalPlayer != null && GameManager.Instance.LocalPlayer.Ready ? "Ready\n" : "Not Ready\n";
            
            if (GameManager.Instance.RedPlayers < 1 && GameManager.Instance.BluePlayers < 1)
            {
                return $"{msg}No players on Red or Blue";
            }

            if (GameManager.Instance.RedPlayers < 1)
            {
                return $"{msg}No players on Red";
            }

            if (GameManager.Instance.BluePlayers < 1)
            {
                return $"{msg}No players on Blue";
            }

            int maxPlayers = GameManager.Instance.maxConnections;
            if (GameManager.Instance.RedPlayers > maxPlayers || GameManager.Instance.BluePlayers > maxPlayers)
            {
                return $"Maximum {maxPlayers} per team";
            }

            if (GameManager.Instance.RedPlayers + GameManager.Instance.BluePlayers != GameManager.Instance.TotalPlayers)
            {
                return $"{msg}Waiting for players to pick teams";
            }

            if (GameManager.Instance.ReadyPlayers == GameManager.Instance.TotalPlayers)
            {
                return $"{msg}Starting match...";
            }

            return $"{msg}{GameManager.Instance.ReadyPlayers} of {GameManager.Instance.TotalPlayers} ready";
        }
    }
}