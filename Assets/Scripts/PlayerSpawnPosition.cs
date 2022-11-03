using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Pickup;
using UnityEngine;
using Random = System.Random;

public class PlayerSpawnPosition : NetworkBehaviour
{
    [SerializeField]
    [Tooltip("The player number this spawn is for.")]
    private int teamMemberIndex;
    
    [SerializeField]
    [Tooltip("The team this spawn is for.")]
    private Team team;
    
    private static List<PlayerSpawnPosition> _redSpawnPositions;
    
    private static List<PlayerSpawnPosition> _blueSpawnPositions;

    private void Awake()
    {
        if (team == Team.None)
        {
            Destroy(gameObject);
            return;
        }
        
        _redSpawnPositions = null;
        _blueSpawnPositions = null;
    }

    public static IEnumerator SpawnPlayer(PlayerController playerController, PlayerController killedBy, Team team, int teamMemberIndex, int delay = 0)
    {
        if (playerController != GameManager.Instance.LocalPlayer || playerController.respawning)
        {
            yield break;
        }
        
        playerController.SetRespawning(true);
        
        List<PlayerSpawnPosition> positions = null;
        switch (team)
        {
            case Team.Red:
                if (_redSpawnPositions == null || _redSpawnPositions.Any(u => u == null))
                {
                    _redSpawnPositions = FindObjectsOfType<PlayerSpawnPosition>().Where(s => s.team == Team.Red).ToList();
                }

                positions = _redSpawnPositions;
                break;
            case Team.Blue:
                if (_blueSpawnPositions == null || _blueSpawnPositions.Any(u => u == null))
                {
                    _blueSpawnPositions = FindObjectsOfType<PlayerSpawnPosition>().Where(s => s.team == Team.Blue).ToList();
                }

                positions = _blueSpawnPositions;
                break;
        }

        if (positions != null && positions.Any())
        {
            FlagPickup flag = null;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (FlagPickup.redFlag != null && FlagPickup.redFlag.carryingPlayer == playerController)
            {
                flag = FlagPickup.redFlag;
                Transform flagTransform = FlagPickup.redFlag.transform;
                position = flagTransform.position;
                rotation = flagTransform.rotation;

            }
            else if (FlagPickup.blueFlag != null && FlagPickup.blueFlag.carryingPlayer == playerController)
            {
                flag = FlagPickup.blueFlag;
                Transform flagTransform = FlagPickup.blueFlag.transform;
                position = flagTransform.position;
                rotation = flagTransform.rotation;
            }
            
            PlayerSpawnPosition playerSpawnPosition = positions.FirstOrDefault(p => p.teamMemberIndex == teamMemberIndex);
            if (playerSpawnPosition == null)
            {
                playerSpawnPosition = positions[new Random().Next(positions.Count)];
            }
            
            playerController.CharacterController.enabled = false;
            playerController.SetHealth(0);
            playerController.canMove = false;

            if (killedBy != null)
            {
                GameManager.Instance.DeathPosition = GameManager.Instance.CameraPosition.position;
                GameManager.Instance.DeathTarget = killedBy;
            }
        
            Transform spawnTransform = playerSpawnPosition.transform;
            playerController.transform.position = spawnTransform.position;
            playerController.transform.rotation = Quaternion.Euler(0, spawnTransform.rotation.eulerAngles.y, 0);
        
            if (flag != null)
            {
                playerController.DropFlag(flag, position, rotation);
            }

            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }
        }

        playerController.CharacterController.enabled = true;
        playerController.SetHealth(PlayerController.PlayerMaxHealth);
        playerController.canMove = true;

        for (int i = 0; i < playerController.weapons.Length; i++)
        {
            playerController.ReplenishServer(i);
        }

        GameManager.Instance.DeathTarget = null;

        playerController.SetRespawning(false);
    }
}