using System.Collections.Generic;
using System.Linq;
using Mirror;
using Mirror.Experimental;
using UnityEngine;

namespace Weapons
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkRigidbody))]
    public class ProjectileBullet : NetworkBehaviour
    {
        [HideInInspector]
        [SyncVar]
        public PlayerController shotBy;

        [HideInInspector]
        [SyncVar]
        public int weaponIndex;
        
        [HideInInspector]
        [SyncVar]
        public float velocity;

        [HideInInspector]
        [SyncVar]
        public bool gravity;
        
        [HideInInspector]
        [SyncVar]
        public int damage;

        [HideInInspector]
        [SyncVar]
        public float distance;
        
        private void Start()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = hasAuthority;
            }

            if (!hasAuthority)
            {
                return;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            rb.AddRelativeForce(Vector3.forward * velocity, ForceMode.VelocityChange);
            rb.useGravity = gravity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleCollision(collision);
        }

        private void HandleCollision(Collision collision)
        {
            if (!hasAuthority)
            {
                return;
            }

            PlayerController attacked;
            Transform tr = collision.transform;
            do
            {
                attacked = tr.GetComponent<PlayerController>();
                tr = tr.parent;
            } while (attacked == null && tr != null);
            if (attacked == GameManager.Instance.LocalPlayer)
            {
                return;
            }
            
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            
            if (attacked != null)
            {
                if (GameManager.Instance.FriendlyFire || attacked.Team != GameManager.Instance.LocalPlayer.Team)
                {
                    GameManager.Instance.LocalPlayer.AttackPlayerCommand(shotBy, attacked, damage);
                }
            }
            
            if (distance > 0)
            {
                int layerMask = LayerMask.GetMask("Default", "HitBox");
                
                PlayerController[] players = FindObjectsOfType<PlayerController>().Where(p => p != GameManager.Instance.LocalPlayer && p != attacked).ToArray();
                if (!GameManager.Instance.FriendlyFire)
                {
                    players = players.Where(p => p.Team != GameManager.Instance.Team).ToArray();
                }

                foreach (PlayerController playerController in players)
                {
                    Collider[] hitBoxes = playerController.GetComponentsInChildren<Collider>().Where(c => c.gameObject.layer == LayerMask.NameToLayer("HitBox")).ToArray();
                    
                    Vector3 position = playerController.transform.position;
                    List<Vector3> points = new List<Vector3> { position, new Vector3(position.x, position.y + 0.1f, position.z), playerController.CameraPosition.position };
                    points.AddRange(hitBoxes.Select(h => h.bounds).Select(b => b.ClosestPoint(transform.position)));
                
                    foreach (Vector3 point in points.Where(p => Vector3.Distance(p, transform.position) <= distance).OrderBy(p => Vector3.Distance(p, transform.position)))
                    {
                        if (!Physics.Linecast(transform.position, point, out RaycastHit hit, layerMask) || !hitBoxes.Contains(hit.collider))
                        {
                            continue;
                        }
                        
                        GameManager.Instance.LocalPlayer.AttackPlayerCommand(shotBy, playerController, Mathf.Max((int) (damage * (1 - Vector3.Distance(point, transform.position) / distance)), 1));

                        break;
                    }
                }
            }
            
            GameManager.Instance.LocalPlayer.DestroyProjectile(this);
        }
    }
}