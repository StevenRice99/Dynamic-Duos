using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Weapons
{
    public class RaycastWeapon : Weapon
    {
        [SerializeField]
        [Tooltip("The material for the bullet trail.")]
        private Material material;

        [SerializeField]
        [Min(1)]
        [Tooltip("How many rounds to fire each shot.")]
        private int rounds;

        [SerializeField]
        [Range(0, 1)]
        [Tooltip("How much spread should the shots have.")]
        private float spread;

        [HideInInspector]
        public LayerMask layerMask;
        
        public override void ShootLocal(out Vector3[] positions, out Quaternion rotation)
        {
            positions = new Vector3[rounds];
            rotation = Quaternion.identity;

            Vector3 forward = GameManager.Instance.CameraPosition.TransformDirection(Vector3.forward);

            List<AttackedInfo> attackedInfos = new List<AttackedInfo>();

            for (int i = 0; i < rounds; i++)
            {
                Vector3 direction = forward + new Vector3(
                    Random.Range(-spread, spread),
                    Random.Range(-spread, spread),
                    Random.Range(-spread, spread)
                );
                direction.Normalize();
                
                if (Physics.Raycast(GameManager.Instance.CameraPosition.position, direction, out RaycastHit hit, Mathf.Infinity, layerMask))
                {
                    positions[i] = hit.point;
                    PlayerController attacked;
                    Transform tr = hit.collider.transform;
                    do
                    {
                        attacked = tr.GetComponent<PlayerController>();
                        tr = tr.parent;
                    } while (attacked == null && tr != null);

                    if (attacked == null)
                    {
                        continue;
                    }

                    if (GameManager.Instance.FriendlyFire || attacked.Team != GameManager.Instance.LocalPlayer.Team)
                    {
                        bool found = false;
                        for (int j = 0; j < attackedInfos.Count; j++)
                        {
                            if (attackedInfos[j].playerController != attacked)
                            {
                                continue;
                            }

                            AttackedInfo attackedInfo = attackedInfos[j];
                            attackedInfo.hits++;
                            attackedInfos[j] = attackedInfo;
                            found = true;
                            break;
                        }

                        if (!found)
                        {
                            attackedInfos.Add(new AttackedInfo { playerController = attacked, hits = 1 });
                        }
                    }
                    
                    continue;
                }

                positions[i] = GameManager.Instance.CameraPosition.position + direction * 1000;
            }

            foreach (AttackedInfo attackedInfo in attackedInfos)
            {
                GameManager.Instance.LocalPlayer.AttackPlayerCommand(GameManager.Instance.LocalPlayer, attackedInfo.playerController, damage * attackedInfo.hits);
            }
        }

        public override void ShootVisuals(Vector3[] positions)
        {
            foreach (Vector3 v in positions)
            {
                GameObject bullet = new GameObject($"{name} Bullet");
                LineRenderer lr = bullet.AddComponent<LineRenderer>();
                lr.material = material;
                lr.startColor = lr.endColor = material.color;
                lr.startWidth = lr.endWidth = 0.025f;
                lr.numCornerVertices = lr.numCapVertices = 90;
                lr.positionCount = 2;
                Vector3 barrelPosition = barrel.position;
                lr.SetPositions(new [] { barrelPosition, v });
                StartCoroutine(FadeLine(lr));

                ImpactAudio(v, positions.Length);
                ImpactVisual(v, barrelPosition);
            }
            
            base.ShootVisuals(positions);
        }

        protected override void Awake()
        {
            base.Awake();
            
            if (time > delay)
            {
                time = delay;
            }
            
            layerMask = LayerMask.GetMask("Default", "Projectile", "HitBox");
        }

        private IEnumerator FadeLine(LineRenderer lr)
        {
            Material mat = lr.material;
            Color color = mat.color;
            float startAlpha = lr.startColor.a;
            float duration = 0;
            while (duration < 1)
            {
                float alpha = startAlpha * (1 - duration);
                mat.color = new Color(color.r, color.g, color.b, alpha);
                duration += Time.deltaTime / time;
                yield return null;
            }
            Destroy(lr.gameObject);
        }
    }
}