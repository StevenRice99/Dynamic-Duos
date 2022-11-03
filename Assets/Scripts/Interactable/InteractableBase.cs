using UnityEngine;

namespace Interactable
{
    public abstract class InteractableBase : MonoBehaviour
    {
        public string DisplayMessage => displayMessage;

        public float ActivationDistance => activationDistance;
    
        [SerializeField]
        [Tooltip("The message to display when in range.")]
        private string displayMessage;
    
        [SerializeField]
        [Tooltip("How far away this can be activated from.")]
        [Min(float.Epsilon)]
        private float activationDistance;

        public abstract void Interact();
    }
}