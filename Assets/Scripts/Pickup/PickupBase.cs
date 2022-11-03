using System.Linq;
using Mirror;
using UnityEngine;

namespace Pickup
{
    [RequireComponent(typeof(Collider))]
    public abstract class PickupBase : NetworkBehaviour
    {
        protected abstract void OnPickedUp(PlayerController playerController, int[] ammo);
        
        [Command(requiresAuthority = false)]
        private void PickedUp(PlayerController playerController, int[] ammo)
        {
            OnPickedUp(playerController, ammo);
        }
    
        private void OnTriggerEnter(Collider other)
        {
            DetectPickup(other);
        }

        private void OnTriggerStay(Collider other)
        {
            DetectPickup(other);
        }

        private void DetectPickup(Component other)
        {
            PlayerController playerController = other.gameObject.GetComponent<PlayerController>();
            if (playerController == null || playerController != GameManager.Instance.LocalPlayer)
            {
                return;
            }

            PickedUp(playerController, playerController.weapons.Select(w => w.ammo).ToArray());
        }
    }
}