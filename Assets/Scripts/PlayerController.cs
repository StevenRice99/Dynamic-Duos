using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Pickup;
using UnityEngine;
using UnityEngine.Rendering;
using Weapons;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerController : NetworkBehaviour
{
    public const int PlayerMaxHealth = 100;

    private const int RespawnDelay = 5;

    private const float WeaponSwitchTime = 1f;
    
    // How fast the player moves, not as a serialized field for easier management to stop accidental changes in editor.
    private const float PlayerSpeed = 15;
    
    // How high the player jumps, not as a serialized field for easier management to stop accidental changes in editor.
    private const float PlayerJumpForce = 6;
    
    // How far to raycast for ground detection, not as a serialized field for easier management to stop accidental changes in editor.
    private const float PlayerGroundDistance = 1.5f;
    
    [SerializeField]
    [Tooltip("Where the camera will be positioned.")]
    private Transform cameraPosition;

    [SerializeField]
    [Tooltip("The head of the player visuals.")]
    private Transform headPosition;

    [SerializeField]
    [Tooltip("Where the first person weapons will be positioned.")]
    private Transform localWeaponPosition;

    [SerializeField]
    [Tooltip("Where the third person weapons will be positioned.")]
    private Transform remoteWeaponPosition;

    [SerializeField]
    [Tooltip("Where the flag will be carried at.")]
    private Transform flagPosition;

    [SerializeField]
    [Tooltip("All meshes that change color based on the team.")]
    private MeshRenderer[] teamColorVisuals;

    [SerializeField]
    [Tooltip("All other visuals that need to be hidden when the player dies.")]
    private MeshRenderer[] otherVisuals;

    public bool Ready => _ready;
    
    public Team Team => _team;

    public int TeamMemberIndex => _teamMemberIndex;
    
    public string PlayerName => _playerName;

    public string WeaponDetails => weapons != null ? weapons[weaponIndex].Details : string.Empty;

    public Transform CameraPosition => cameraPosition;

    public Transform LocalWeaponPosition => localWeaponPosition;

    public Transform FlagPosition => flagPosition;

    public CharacterController CharacterController => _characterController != null ? _characterController : GetComponent<CharacterController>();

    [HideInInspector]
    public Weapon[] weapons;

    [HideInInspector]
    public bool switchingWeapons;

    [HideInInspector]
    [SyncVar(hook = nameof(OnWeaponSwitch))]
    public int weaponIndex;

    [HideInInspector]
    [SyncVar(hook = nameof(OnHealthChange))]
    public int health;

    [HideInInspector]
    [SyncVar(hook = nameof(OnGameStateChange))]
    public GameState gameState = GameState.Playing;

    [HideInInspector]
    [SyncVar]
    public bool respawning;

    [HideInInspector]
    [SyncVar]
    public bool canMove;

    [SyncVar(hook = nameof(OnTeamChange))]
    private Team _team = Team.None;

    [SyncVar(hook = nameof(OnTeamMemberIndexChange))]
    private int _teamMemberIndex;

    [SyncVar]
    private string _playerName;

    [SyncVar(hook = nameof(OnFriendlyFireChange))]
    private bool _friendlyFire;
    
    [SyncVar]
    private bool _ready;
    
    private CharacterController _characterController;

    private CapsuleCollider _capsuleCollider;

    private float _rotationYLocal;
    
    [SyncVar]
    private float _rotationYRemote;

    private float _velocityY;
    
    private bool _falling;

    [Command(requiresAuthority = false)]
    public void CommandMessage(string text)
    {
        ReceiveMessage(text);
    }

    [ClientRpc]
    private void ReceiveMessage(string text)
    {
        GameManager.Instance.AddMessage(text);
    }

    [Command(requiresAuthority = false)]
    public void SetHealth(int value)
    {
        health = Mathf.Clamp(value, 0, PlayerMaxHealth);
    }
    
    [Command(requiresAuthority = false)]
    public void ReplenishServer(int weaponIndex)
    {
        ReplenishCore(weaponIndex);
        ReplenishClient(weaponIndex);
    }

    [ClientRpc]
    private void ReplenishClient(int weaponIndex)
    {
        if (isServer)
        {
            return;
        }
        
        ReplenishCore(weaponIndex);
    }

    private void ReplenishCore(int weaponIndex)
    {
        weapons[weaponIndex].Replenish();
    }
    

    [Command]
    public void SetRespawning(bool value)
    {
        respawning = value;
    }

    [Command]
    public void SwitchWeapon(int weaponIndex)
    {
        this.weaponIndex = Mathf.Clamp(weaponIndex, 0, weapons.Length - 1);
    }

    [Command]
    public void SwapWeapon(int swap)
    {
        if (swap == 0)
        {
            return;
        }
        
        int index = weaponIndex;
        if (swap > 0)
        {
            index++;
            if (index >= weapons.Length)
            {
                index = 0;
            }
        }
        else
        {
            index--;
            if (index < 0)
            {
                index = weapons.Length - 1;
            }
        }

        weaponIndex = index;
    }

    [Command(requiresAuthority = false)]
    public void AttackPlayerCommand(PlayerController attacker, PlayerController attacked, int damage)
    {
        int newHealth = attacked.health - damage;
        if (newHealth > 0)
        {
            attacked.SetHealth(Mathf.Clamp(newHealth, 0, PlayerMaxHealth));
            return;
        }
        
        CommandMessage($"{attacker._playerName} killed {attacked._playerName}");
        if (FlagPickup.redFlag != null && FlagPickup.redFlag.carryingPlayer == attacked)
        {
            CommandMessage($"{attacked._playerName} dropped the Red Flag");
        }
        else if (FlagPickup.blueFlag != null && FlagPickup.blueFlag.carryingPlayer == attacked)
        {
            CommandMessage($"{attacked._playerName} dropped the Blue Flag");
        }
        
        NetworkIdentity identity = attacked.GetComponent<NetworkIdentity>();
        AttackPlayerRemote(identity.connectionToClient, attacker, attacked, damage);
    }

    [TargetRpc]
    private void AttackPlayerRemote(NetworkConnection connection, PlayerController attacker, PlayerController attacked, int damage)
    {
        StartCoroutine(PlayerSpawnPosition.SpawnPlayer(attacked, attacker, attacked._team, attacked._teamMemberIndex, RespawnDelay));
    }

    [Command]
    public void DropFlag(FlagPickup flag, Vector3 position, Quaternion rotation)
    {
        flag.DropFlag(true, position, rotation);
    }

    [Command]
    public void SetReady(bool ready)
    {
        _ready = ready;
    }

    [Command]
    public void SetTeam(Team team)
    {
        if (team == Team.None)
        {
            team = FindObjectsOfType<PlayerController>().Count(p => p.Team == Team.Red) <= FindObjectsOfType<PlayerController>().Count(p => p.Team == Team.Blue) ? Team.Red : Team.Blue;
        }
        
        _team = team;
        
        int index = 0;
        IEnumerable<PlayerController> players = FindObjectsOfType<PlayerController>().Where(p => p.Team == team).OrderBy(p => p._playerName);
        foreach (PlayerController player in players)
        {
            player._teamMemberIndex = index++;
        }
    }

    [Command]
    public void SetFriendlyFire(bool friendlyFire)
    {
        _friendlyFire = friendlyFire;
    }

    private void OnTeamChange(Team oldTeam, Team newTeam)
    {
        if (isLocalPlayer)
        {
            GameManager.Instance.SetTeam();
        }
        
        UpdateMaterials();
    }

    private void OnTeamMemberIndexChange(int oldIndex, int newIndex)
    {
        if (isLocalPlayer)
        {
            GameManager.Instance.SetTeamMemberIndex();
        }
    }

    public void OnFriendlyFireChange(bool oldFriendlyFire, bool newFriendlyFire)
    {
        foreach (PlayerController playerController in FindObjectsOfType<PlayerController>())
        {
            playerController._friendlyFire = newFriendlyFire;
        }
        
        GameManager.Instance.SetFriendlyFire(newFriendlyFire);
    }

    private void OnGameStateChange(GameState oldGameState, GameState newGameState)
    {
        foreach (PlayerController playerController in FindObjectsOfType<PlayerController>())
        {
            playerController.gameState = newGameState;
        }
    }

    public void OnWeaponSwitch(int oldIndex, int newIndex)
    {
        StartCoroutine(WeaponSwitchTimer());
        for (int i = 0; i < weapons.Length; i++)
        {
            bool active = i == newIndex && _team != Team.None;
            weapons[i].Visible(active);
        }
    }

    private IEnumerator WeaponSwitchTimer()
    {
        switchingWeapons = true;
        yield return new WaitForSeconds(WeaponSwitchTime);
        switchingWeapons = false;
    }

    [Command]
    private void ShootWeaponCommand(int weaponIndex, Vector3[] positions, Quaternion rotation)
    {
        ShootWeaponRemote(weaponIndex, positions, rotation);
    }

    [ClientRpc]
    private void ShootWeaponRemote(int weaponIndex, Vector3[] positions, Quaternion rotation)
    {
        if (isLocalPlayer)
        {
            return;
        }

        weapons[weaponIndex].ShootVisuals(positions);
    }

    [Command(requiresAuthority = false)]
    public void ShootProjectile(PlayerController shotBy, int weaponIndex, int bulletIndex, int damage, float distance, float velocity, bool gravity, Vector3 position, Quaternion rotation, float time)
    {
        GameObject bullet = Instantiate(GameManager.Instance.spawnPrefabs[bulletIndex], position, rotation);
        bullet.name = $"{name} Bullet";
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        ProjectileBullet projectileBullet = bullet.GetComponent<ProjectileBullet>();
        projectileBullet.weaponIndex = weaponIndex;
        projectileBullet.shotBy = shotBy;
        projectileBullet.damage = damage;
        projectileBullet.distance = distance;
        projectileBullet.velocity = velocity;
        rb.useGravity = projectileBullet.gravity = gravity;
        rb.AddRelativeForce(Vector3.forward * velocity, ForceMode.VelocityChange);
        Destroy(bullet, time);
        NetworkServer.Spawn(bullet, GetComponent<NetworkIdentity>().connectionToClient);
    }

    [Command(requiresAuthority = false)]
    public void DestroyProjectile(ProjectileBullet bullet)
    {
        if (bullet == null)
        {
            return;
        }

        ProjectileImpact(bullet.shotBy, bullet.weaponIndex, bullet.transform.position);
        NetworkServer.Destroy(bullet.gameObject);
    }

    [ClientRpc]
    private void ProjectileImpact(PlayerController shotBy, int weaponIndex, Vector3 p)
    {
        shotBy.weapons[weaponIndex].ImpactAudio(p, 1);
        shotBy.weapons[weaponIndex].ImpactVisual(p, Vector3.zero);
    }

    private void OnHealthChange(int oldHealth, int newHealth)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (health <= 0)
            {
                col.enabled = false;
                continue;
            }

            if (col == _capsuleCollider)
            {
                col.enabled = !isLocalPlayer;
                continue;
            }

            col.enabled = true;
        }
        
        ShadowCastingMode shadowCastingMode = newHealth > 0
            ? isLocalPlayer ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On
            : ShadowCastingMode.Off;

        bool meshEnabled = health > 0;
        
        foreach (MeshRenderer meshRenderer in teamColorVisuals)
        {
            meshRenderer.shadowCastingMode = shadowCastingMode;
            meshRenderer.enabled = meshEnabled;
        }

        foreach (MeshRenderer meshRenderer in  otherVisuals)
        {
            meshRenderer.shadowCastingMode = shadowCastingMode;
            meshRenderer.enabled = meshEnabled;
        }

        if (weapons == null)
        {
            return;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].Visible(meshEnabled && i == weaponIndex);
        }
    }

    private void Awake()
    {
        if (!GameManager.Instance.IsLobby && GameManager.Instance.Team == Team.None)
        {
            GameManager.Instance.StopClient();
        }
    }

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _characterController.enabled = isLocalPlayer;
        _capsuleCollider.enabled = !isLocalPlayer;
        
        UpdateMaterials();

        weapons = GetComponentsInChildren<Weapon>();
        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].index = i;
            weapons[i].AssignLocal(isLocalPlayer);
            weapons[i].Visible(i == weaponIndex);
        }
    }

    public override void OnStartLocalPlayer()
    {
        GameManager.Instance.SetLocalPlayer(this);
        SetName(GameManager.Instance.PlayerName);
        SetTeam(GameManager.Instance.Team);
        GetFriendlyFire();
        GetGameState();

        int layer = LayerMask.NameToLayer("LocalPlayer");
        foreach (Transform tr in GetComponentsInChildren<Transform>())
        {
            tr.gameObject.layer = layer;
        }

        StartCoroutine(PlayerSpawnPosition.SpawnPlayer(this, null, GameManager.Instance.Team, GameManager.Instance.TeamMemberIndex));
        health = PlayerMaxHealth;
        OnHealthChange(health, health);

        if (!isServer || GameManager.Instance.IsLobby)
        {
            return;
        }

        StartCountdownCommand();
    }

    [Command]
    private void StartCountdownCommand()
    {
        StartCoroutine(StartCountdown());
    }
    
    private IEnumerator StartCountdown()
    {
        gameState = GameState.Starting;
        yield return new WaitForSeconds(GameManager.SceneSwitchDelay);
        gameState = GameState.Playing;
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            remoteWeaponPosition.localRotation = Quaternion.Euler(_rotationYRemote, 0, 0);;

            headPosition.localRotation = Quaternion.Euler(Mathf.Clamp(_rotationYRemote, -45, 45), 0, 0);
            
            return;
        }
        
        Movement();
        
        Shooting();
    }

    private void Movement()
    {
        if (!_characterController.enabled)
        {
            return;
        }
        
        transform.Rotate(0, GameManager.Instance.Look.x * GameManager.Instance.Sensitivity * Time.deltaTime, 0);

        _rotationYLocal = Mathf.Clamp(
            _rotationYLocal + -GameManager.Instance.Look.y * GameManager.Instance.Sensitivity * Time.deltaTime,
            -90,
            90
        );

        UpdateLookRotationCommand(_rotationYLocal);
        
        cameraPosition.transform.localRotation = Quaternion.Euler(_rotationYLocal, 0, 0);

        bool grounded = _characterController.isGrounded;
        
        if (grounded)
        {
            _velocityY = 0;
            
            if (canMove && gameState == GameState.Playing && GameManager.Instance.Jump)
            {
                _falling = true;
                _velocityY += Mathf.Sqrt(-PlayerJumpForce * Physics.gravity.y);
            }
            else
            {
                _falling = false;
            }
        }
        
        if (!_falling)
        {
            Vector3 castStartPos = transform.position + new Vector3(0, _characterController.center.y, 0);
            
            if (Physics.Raycast(castStartPos, Vector3.down, PlayerGroundDistance))
            {
                _velocityY = Physics.gravity.y;
            }
            else
            {
                _falling = true;
            }
        }

        _velocityY += Physics.gravity.y * Time.deltaTime;
        
        Transform tr = transform;
        Vector3 targetVelocity = canMove && gameState == GameState.Playing ? (GameManager.Instance.Move.y * tr.forward + GameManager.Instance.Move.x * tr.right) * PlayerSpeed : Vector3.zero;
        targetVelocity.y = _velocityY;
        _characterController.Move(targetVelocity * Time.deltaTime);
    }

    [Command]
    private void UpdateLookRotationCommand(float rotationY)
    {
        _rotationYRemote = rotationY;
    }

    private void Shooting()
    {
        if (gameState != GameState.Playing || switchingWeapons || GameManager.Instance.IsLobby || !canMove || !GameManager.Instance.Shoot)
        {
            return;
        }
        
        Weapon weapon = weapons[weaponIndex];
        
        if (!weapon.CanShoot || weapon.ammo == 0)
        {
            return;
        }

        weapon.ShootLocal(out Vector3[] positions, out Quaternion rotation);
        weapon.ShootVisuals(positions);
        weapon.StartDelay();
        ShootWeaponCommand(weaponIndex, positions, rotation);
    }

    [Command]
    private void SetName(string playerName)
    {
        _playerName = playerName;
    }

    private void UpdateMaterials()
    {
        Material material;
        switch (_team)
        {
            case Team.None:
                material = GameManager.Instance.NoneMaterial;
                break;
            case Team.Red:
                material = GameManager.Instance.RedTeamMaterial;
                break;
            case Team.Blue:
                material = GameManager.Instance.BlueTeamMaterial;
                break;
            default:
                Debug.LogError("Invalid team given.");
                return;
        }
        
        foreach (MeshRenderer meshRenderer in teamColorVisuals)
        {
            meshRenderer.material = material;
        }
    }

    private void GetFriendlyFire()
    {
        if (isServer)
        {
            _friendlyFire = GameManager.Instance.FriendlyFire;
            return;
        }

        int trueFalse = 0;

        foreach (PlayerController playerController in FindObjectsOfType<PlayerController>())
        {
            if (playerController.isLocalPlayer)
            {
                continue;
            }

            if (playerController._friendlyFire)
            {
                trueFalse++;
            }
            else
            {
                trueFalse--;
            }
        }

        GameManager.Instance.SetFriendlyFire(_friendlyFire = trueFalse == 0 ? _friendlyFire : trueFalse > 0);
    }

    private void GetGameState()
    {
        if (isServer)
        {
            return;
        }

        int starting = 0;
        int playing = 0;
        int ending = 0;

        foreach (PlayerController playerController in FindObjectsOfType<PlayerController>())
        {
            if (playerController.isLocalPlayer)
            {
                continue;
            }
            
            switch (playerController.gameState)
            {
                case GameState.Starting:
                    starting++;
                    break;
                case GameState.Playing:
                    playing++;
                    break;
                case GameState.Ending:
                    ending++;
                    break;
            }
        }

        if (starting >= playing && starting >= ending)
        {
            gameState = GameState.Starting;
            return;
        }

        if (ending > starting && ending >= playing)
        {
            gameState = GameState.Ending;
            return;
        }

        gameState = GameState.Playing;
    }
}