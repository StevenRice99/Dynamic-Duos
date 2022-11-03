namespace Interactable
{
    public class ReadyInteractable : InteractableBase
    {
        public override void Interact()
        {
            GameManager.Instance.LocalPlayer.SetReady(!GameManager.Instance.LocalPlayer.Ready);
        }
    }
}