using UnityEngine;

namespace Weapons
{
    public class ProjectileWeapon : Weapon
    {
        [SerializeField]
        [Tooltip("How fast the projectile should travel.")]
        private float velocity = 10;

        [SerializeField]
        [Tooltip("Splash damage distance.")]
        private float distance;
        
        [SerializeField]
        [Tooltip("The index of the projectile prefab in the GameManager to spawn.")]
        private int bulletIndex;

        [SerializeField]
        [Tooltip("If the projectile is affected by gravity.")]
        private bool gravity;
        
        public override void ShootLocal(out Vector3[] positions, out Quaternion rotation)
        {
            positions = new[] { barrel.position };
            rotation = barrel.rotation;

            GameManager.Instance.LocalPlayer.ShootProjectile(GameManager.Instance.LocalPlayer, index, bulletIndex, damage, distance, velocity, gravity, GameManager.Instance.CameraPosition.position, GameManager.Instance.CameraPosition.rotation, time);
        }
    }
}