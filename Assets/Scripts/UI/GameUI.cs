using Pickup;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        private Label _redScoreLabel;

        private Label _blueScoreLabel;

        private Label _displayMessageLabel;

        private Label _healthAndAmmoLabel;

        private VisualElement _crosshair;

        private void Start()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            
            _redScoreLabel = root.Q<Label>("RedScoreLabel");
            _blueScoreLabel = root.Q<Label>("BlueScoreLabel");
            _displayMessageLabel = root.Q<Label>("DisplayMessage");
            _healthAndAmmoLabel = root.Q<Label>("HealthAndAmmoLabel");
            _crosshair = root.Q<VisualElement>("Crosshair");
        }

        private void Update()
        {
            _redScoreLabel.text = (FlagPickup.blueFlag != null ? FlagPickup.blueFlag.Captures : 0).ToString();
            _blueScoreLabel.text = (FlagPickup.redFlag != null ? FlagPickup.redFlag.Captures : 0).ToString();

            PlayerController localPlayer = GameManager.Instance.LocalPlayer;
            if (localPlayer == null)
            {
                _healthAndAmmoLabel.text = string.Empty;
                _displayMessageLabel.text = string.Empty;
                _crosshair.visible = false;
                return;
            }

            _crosshair.visible = GameManager.Instance.DeathTarget == null;

            switch (localPlayer.gameState)
            {
                case GameState.Starting:
                    _displayMessageLabel.text = "Round will begin soon...";
                    _healthAndAmmoLabel.text = string.Empty;
                    _crosshair.style.backgroundColor = Color.red;
                    break;
                case GameState.Playing:
                    _displayMessageLabel.text = GameManager.Instance.GameMessages;
                    string flagString = FlagPickup.redFlag.carryingPlayer == localPlayer || FlagPickup.blueFlag.carryingPlayer == localPlayer ? "Carrying Flag\n" : string.Empty;
                    _healthAndAmmoLabel.text = localPlayer.health > 0 ? $"{flagString}Health: {localPlayer.health}\n{localPlayer.WeaponDetails}" : $"Killed by {GameManager.Instance.DeathTarget.PlayerName}";
                    _crosshair.style.backgroundColor = localPlayer.switchingWeapons || !localPlayer.weapons[localPlayer.weaponIndex].CanShoot || localPlayer.weapons[localPlayer.weaponIndex].ammo == 0 || GameManager.Instance.Shoot ? Color.red : Color.green;
                    break;
                case GameState.Ending:
                    string msg = string.Empty;
                    if (FlagPickup.redFlag != null && FlagPickup.redFlag.Captures >= FlagPickup.CapturesToWin)
                    {
                        msg = "Blue team wins!\n";
                    }
                    else if (FlagPickup.blueFlag != null && FlagPickup.blueFlag.Captures >= FlagPickup.CapturesToWin)
                    {
                        msg = "Red team wins!\n";
                    }
                    msg += "Next round will begin soon...";
                    _displayMessageLabel.text = msg;
                    _healthAndAmmoLabel.text = string.Empty;
                    _crosshair.style.backgroundColor = Color.red;
                    break;
            }
        }
    }
}