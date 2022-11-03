namespace Interactable
{
    public class ToggleFriendlyFireInteractable : InteractableBase
    {
        public override void Interact()
        {
            GameManager.Instance.LocalPlayer.SetFriendlyFire(!GameManager.Instance.FriendlyFire);
        }
    }
}