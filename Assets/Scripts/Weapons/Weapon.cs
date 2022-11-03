using System.Collections;
using UnityEngine;

namespace Weapons
{
    public abstract class Weapon : MonoBehaviour
    {
        public bool CanShoot { get; private set; } = true;

        [HideInInspector]
        public int ammo;

        [Tooltip("The maximum ammo of the weapon, setting to less than 0 will give unlimited ammo.")]
        public int maxAmmo = -1;

        [HideInInspector]
        public int index;

        [HideInInspector]
        public AudioSource shootSound;

        [Tooltip("The sound to make upon bullet impact.")]
        public AudioClip impactSound;

        [Tooltip("The effect prefab to show upon bullet impact.")]
        public GameObject impactEffectPrefab;
        
        [SerializeField]
        [Tooltip("The barrel of the weapon.")]
        protected Transform barrel;
        
        [SerializeField]
        [Min(1)]
        [Tooltip("How much damage the weapon should do.")]
        protected int damage;
        
        [SerializeField]
        [Min(0)]
        [Tooltip("How long between shots should there be.")]
        protected float delay;
        
        [SerializeField]
        [Min(0)]
        [Tooltip("How long bullet trails or projectiles last for.")]
        protected float time;

        private bool _localWeapon;
        
        private MeshRenderer[] _renderers;

        public string Details => name + (ammo >= 0 ? $": {ammo}" : string.Empty);

        public void Replenish()
        {
            ammo = maxAmmo;
        }

        public void AssignLocal(bool localWeapon)
        {
            _localWeapon = localWeapon;

            foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.gameObject.layer = localWeapon ? LayerMask.NameToLayer("LocalWeapons") : LayerMask.NameToLayer("Default");
            }

            if (!_localWeapon)
            {
                return;
            }

            if (shootSound == null)
            {
                shootSound = GetComponent<AudioSource>();
            }

            shootSound.spatialBlend = 0;
            shootSound.rolloffMode = AudioRolloffMode.Linear;
            shootSound.minDistance = 1000;
            shootSound.maxDistance = 2000;
        }

        public void StartDelay()
        {
            if (ammo > 0)
            {
                ammo--;
            }
            
            StartCoroutine(ShotDelay());
        }

        public void Visible(bool visible)
        {
            _renderers ??= GetComponentsInChildren<MeshRenderer>();

            visible = visible && !GameManager.Instance.IsLobby;

            foreach (MeshRenderer meshRenderer in _renderers)
            {
                meshRenderer.enabled = visible;
            }
        }

        public abstract void ShootLocal(out Vector3[] positions, out Quaternion rotation);

        public virtual void ShootVisuals(Vector3[] positions)
        {
            shootSound.Play();
        }

        public void ImpactAudio(Vector3 p, int numImpacts)
        {
            GameObject impactObj = new GameObject($"{name} Audio")
            {
                transform =
                {
                    position = p
                }
            };
            AudioSource impact = impactObj.AddComponent<AudioSource>();
            impact.clip = impactSound;
            impact.volume = GameManager.Instance.Sound / numImpacts;
            impact.spatialBlend = 1;
            impact.dopplerLevel = shootSound.dopplerLevel;
            impact.spread = shootSound.spread;
            impact.rolloffMode = shootSound.rolloffMode;
            impact.minDistance = shootSound.minDistance;
            impact.maxDistance = shootSound.maxDistance;
            impact.Play();
            Destroy(impactObj, impactSound.length);
        }

        public void ImpactVisual(Vector3 p, Vector3 lookAt)
        {
            GameObject effect = Instantiate(impactEffectPrefab, p, Quaternion.identity);
            if (lookAt == Vector3.zero)
            {
                effect.transform.rotation = Quaternion.Euler(Random.Range(0, 360), 0, Random.Range(0, 360));
            }
            else
            {
                effect.transform.LookAt(lookAt);
            }
            effect.name = $"{name} Effect";
            Destroy(effect, effect.GetComponent<ParticleSystem>().main.duration);
        }

        protected virtual void Awake()
        {
            ammo = maxAmmo;
        }

        private void Start()
        {
            shootSound = GetComponent<AudioSource>();
            shootSound.volume = GameManager.Instance.Sound;
        }

        private void Update()
        {
            if (!_localWeapon)
            {
                return;
            }

            Transform tr = transform;
            tr.position = GameManager.Instance.LocalPlayer.LocalWeaponPosition.position;
            tr.rotation = GameManager.Instance.LocalPlayer.LocalWeaponPosition.rotation;
        }

        private IEnumerator ShotDelay()
        {
            CanShoot = false;
            yield return new WaitForSeconds(delay);
            CanShoot = true;
        }
    }
}