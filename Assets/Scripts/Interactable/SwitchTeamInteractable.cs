using UnityEngine;

namespace Interactable
{
    public class SwitchTeamInteractable : InteractableBase
    {
        [SerializeField]
        [Tooltip("What team to switch to when interacted with.")]
        private Team team;
        
        public override void Interact()
        {
            GameManager.Instance.LocalPlayer.SetTeam(team);
        }
    }
}