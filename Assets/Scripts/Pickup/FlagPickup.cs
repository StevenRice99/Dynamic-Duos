using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Pickup
{
    public class FlagPickup : PickupBase
    {
        public const int CapturesToWin = 10;
        
        private const float CaptureDistance = 1f;
        
        public static FlagPickup blueFlag;

        public static FlagPickup redFlag;

        public int Captures => _captures;

        [HideInInspector]
        [SyncVar]
        public PlayerController carryingPlayer;
        
        [SerializeField]
        [Tooltip("The flag mesh itself that will be given color.")]
        private MeshRenderer flagRenderer;
        
        [SerializeField]
        [Tooltip("The team this flag is for.")]
        private Team team;

        [SerializeField]
        [Tooltip("The raycast for a dropped flag to hit the ground.")]
        private LayerMask raycastMask;

        private Vector3 _spawnPosition;

        private Quaternion _spawnRotation;

        [SyncVar]
        private Vector3 _dropPosition;

        [SyncVar]
        private Quaternion _dropRotation;

        [SyncVar]
        private int _captures;

        private bool _beingCarried;

        private Coroutine _captureDelay;

        public void DropFlag(bool beingCarried, Vector3 position, Quaternion rotation)
        {
            carryingPlayer = null;
            
            Transform tr = transform;
            if (tr.position == _spawnPosition && tr.rotation == _spawnRotation)
            {
                _beingCarried = false;
                return;
            }

            if (beingCarried)
            {
                _beingCarried = false;

                if (isServer && Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out RaycastHit hit, Mathf.Infinity, raycastMask))
                {
                    _dropPosition = new Vector3(position.x, hit.point.y, position.z);
                }

                _dropRotation = rotation;
            }
                
            tr.position = _dropPosition;
            tr.rotation = _dropRotation;
        }

        protected override void OnPickedUp(PlayerController playerController, int[] ammo)
        {
            if (carryingPlayer != null)
            {
                return;
            }

            string msg = null;

            if (playerController.Team == team && transform.position != _spawnPosition && transform.rotation != _spawnRotation)
            {
                msg = $"{playerController.PlayerName} returned the {team} Flag";
                ReturnFlag();
            }
            else if (!_beingCarried && playerController.Team != team && playerController.Team != Team.None)
            {
                FlagPickup otherFlag = team == Team.Blue ? redFlag : blueFlag;
                if (Vector3.Distance(playerController.transform.position, otherFlag._spawnPosition) > CaptureDistance)
                {
                    msg = $"{playerController.PlayerName} grabbed the {team} Flag";
                }
                carryingPlayer = playerController;
            }

            if (msg == null)
            {
                return;
            }
            
            GameManager.Instance.LocalPlayer.CommandMessage(msg);
        }

        private void Awake()
        {
            if (team == Team.None || team == Team.Blue && blueFlag != null && blueFlag != this || team == Team.Red && redFlag != null && redFlag != this)
            {
                if (isServer)
                {
                    NetworkServer.Destroy(gameObject);
                }
                
                return;
            }

            Transform tr = transform;
            _dropPosition = _spawnPosition = tr.position;
            _dropRotation = _spawnRotation = tr.rotation;

            if (team == Team.Blue)
            {
                blueFlag = this;
            }
            else
            {
                redFlag = this;
            }
        }

        private void Start()
        {
            flagRenderer.material = team == Team.Blue ? GameManager.Instance.BlueTeamMaterial : GameManager.Instance.RedTeamMaterial;
        }

        private void Update()
        {
            if (carryingPlayer == null)
            {
                DropFlag(_beingCarried, _dropPosition, _dropRotation);

                return;
            }
            
            _beingCarried = true;

            Transform tr = transform;
            tr.position = carryingPlayer.FlagPosition.position;
            tr.rotation = carryingPlayer.FlagPosition.rotation;

            if (!isServer)
            {
                return;
            }

            _dropPosition = tr.position;
            _dropRotation = tr.rotation;

            FlagPickup otherFlag = team == Team.Blue ? redFlag : blueFlag;
            if (Vector3.Distance(carryingPlayer.transform.position, otherFlag._spawnPosition) > CaptureDistance)
            {
                return;
            }
            
            carryingPlayer.CommandMessage($"{carryingPlayer.PlayerName} captured the {team} Flag");

            ReturnFlag(true);
        }

        private void ReturnFlag(bool captured = false)
        {
            Transform tr = transform;
            tr.position = _dropPosition = _spawnPosition;
            tr.rotation = _dropRotation = _spawnRotation;
            carryingPlayer = null;

            if (!captured || _captureDelay != null)
            {
                return;
            }

            _captureDelay = StartCoroutine(CaptureDelay());

            _captures++;
            if (_captures > CapturesToWin)
            {
                _captures = CapturesToWin;
            }
            _beingCarried = false;

            if (_captures >= CapturesToWin)
            {
                StartCoroutine(LoadMap());
            }
        }

        private IEnumerator LoadMap()
        {
            GameEnding();
            yield return new WaitForSeconds(GameManager.SceneSwitchDelay);
            GameManager.Instance.LoadMap();
        }

        [ClientRpc]
        private void GameEnding()
        {
            foreach (PlayerController playerController in FindObjectsOfType<PlayerController>())
            {
                playerController.gameState = GameState.Ending;
            }
        }

        private IEnumerator CaptureDelay()
        {
            yield return new WaitForSeconds(3);
            _captureDelay = null;
        }
    }
}