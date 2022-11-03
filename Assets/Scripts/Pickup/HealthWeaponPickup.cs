using System.Collections;
using Mirror;
using UnityEngine;

namespace Pickup
{
    public class HealthWeaponPickup : PickupBase
    {
        private const float SPEED = 180;
        
        [SerializeField]
        [Tooltip("Set to below 0 to be a health pickup, otherwise the weapon index of the player.")]
        private int weaponIndex = -1;

        [SerializeField]
        [Tooltip("The visuals object to rotate.")]
        private Transform visuals;
        
        private const int DELAY = 10;
        
        [SyncVar(hook = nameof(ToggleMeshes))]
        private bool _ready = true;
            
        private MeshRenderer[] _meshRenderers;
        
        private void Start()
        {
            _meshRenderers = GetComponentsInChildren<MeshRenderer>();
        }

        private void Update()
        {
            visuals.Rotate(0, SPEED * Time.deltaTime, 0, Space.Self);
        }

        protected override void OnPickedUp(PlayerController playerController, int[] ammo)
        {
            if (!_ready)
            {
                return;
            }

            if (weaponIndex < 0)
            {
                if (playerController.health >= PlayerController.PlayerMaxHealth)
                {
                    return;
                }
            
                playerController.SetHealth(PlayerController.PlayerMaxHealth);
                StartCoroutine(ReadyDelay());

                return;
            }

            if (playerController.weapons.Length <= weaponIndex)
            {
                return;
            }

            if (playerController.weapons[weaponIndex].maxAmmo < 0)
            {
                return;
            }

            if (ammo[weaponIndex] >= playerController.weapons[weaponIndex].maxAmmo)
            {
                return;
            }
            
            playerController.ReplenishServer(weaponIndex);
            StartCoroutine(ReadyDelay());
        }

        private IEnumerator ReadyDelay()
        {
            SetReady(false);
            yield return new WaitForSeconds(DELAY);
            SetReady(true);
        }

        [Command(requiresAuthority = false)]
        private void SetReady(bool ready)
        {
            _ready = ready;
        }

        private void ToggleMeshes(bool oldReady, bool newReady)
        {
            foreach (MeshRenderer meshRenderer in _meshRenderers)
            {
                meshRenderer.enabled = newReady;
            }
        }
    }
}