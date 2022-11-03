using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Interactable;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

[RequireComponent(typeof(PlayerInput))]
public class GameManager : NetworkManager
{
    public const int SceneSwitchDelay = 5;
    
    public const string DefaultAddress = "localhost";
    
    public const string DefaultPlayerName = "Anonymous";

    private const float DefaultSensitivity = 10;
    
    private const string FullscreenKey = "Fullscreen";
    
    private const string WidthKey = "Width";
    
    private const string HeightKey = "Height";

    private const int DefaultResolution = 512;

    private const int DefaultAudio = 0;

    private const int NumberMessages = 3;
    
    private const int MessageTime = 5;

    [HideInInspector]
    public Camera cam;

    public static GameManager Instance => singleton as GameManager;
    
    public PlayerController DeathTarget { get; set; }
    
    public Vector3 DeathPosition { get; set; }

    public PlayerController LocalPlayer { get; private set; }

    public Transform CameraPosition => LocalPlayer != null ? LocalPlayer.CameraPosition : null;
    
    public bool FriendlyFire { get; private set; }

    public string DisplayMessage { get; private set; } = string.Empty;

    public string PlayerName { get; private set; } = DefaultPlayerName;

    public Team Team { get; private set; }

    public int TeamMemberIndex { get; private set; }

    public int TotalPlayers { get; private set; }
    
    public int ReadyPlayers { get; private set; }
    
    public int RedPlayers { get; private set; }
    
    public int BluePlayers { get; private set; }

    public float Sound { get; private set; }

    public Material NoneMaterial => noneMaterial;

    public Material RedTeamMaterial => redTeamMaterial;

    public Material BlueTeamMaterial => blueTeamMaterial;

    public Vector2 Move => _optionsOpen ? Vector2.zero : _move;
    
    public Vector2 Look => _optionsOpen ? Vector2.zero : _look;

    public bool IsLobby => networkSceneName == onlineScene || SceneManager.GetActiveScene().name == "Lobby";

    public string GameMessages => _messages.Aggregate(string.Empty, (current, message) => current + message.text + "\n");
    
    public bool Jump => !_optionsOpen && _jump;

    public bool Shoot => !_optionsOpen && _shoot;

    public float Sensitivity => _optionsOpen ? 0 : _sensitivity;

    [Header("Gameplay Settings")]
    
    [SerializeField]
    [Tooltip("Default material for players not assigned to a team.")]
    private Material noneMaterial;
    
    [SerializeField]
    [Tooltip("Material for the red team.")]
    private Material redTeamMaterial;
    
    [SerializeField]
    [Tooltip("Material for the blue team.")]
    private Material blueTeamMaterial;

    [SerializeField]
    [Tooltip("Names of all the maps so map rotation knows what to load.")]
    private string[] maps;

    private Vector2 _move;

    private Vector2 _look;

    private bool _jump;

    private bool _shoot;

    private float _sensitivity;

    private float _music;

    private bool _interact;

    private VisualElement _optionsRoot;

    private TextField _sensitivityTextField;
    
    private Button _closeButton;
    
    private Button _lobbyButton;
    
    private Button _leaveButton;

    private Slider _soundSlider;

    private Slider _musicSlider;

    private bool _optionsOpen;

    private List<UIDocument> _otherUIDocuments;

    private List<Message> _messages = new List<Message>();

    private Coroutine _startMapCoroutine;

    private AudioSource _musicSource;

    private bool IsServer => isNetworkActive && (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.ServerOnly);

    private bool IsClient => isNetworkActive && (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.ClientOnly);

    private IEnumerable<UIDocument> OtherUIDocuments
    {
        get
        {
            if (_otherUIDocuments != null && _otherUIDocuments.All(u => u != null))
            {
                return _otherUIDocuments;
            }

            UIDocument thisUIDocument = GetComponent<UIDocument>();
            _otherUIDocuments = FindObjectsOfType<UIDocument>().Where(u => u != thisUIDocument).ToList();
            return _otherUIDocuments;
        }
    }

    public void AddMessage(string text)
    {
        _messages.Add(new Message {text = text, time = MessageTime});
    }

    public void LoadMap()
    {
        if (maps == null || maps.Length == 0)
        {
            return;
        }

        int index = Array.IndexOf(maps, SceneManager.GetActiveScene().name);
        if (index < 0 || index == maps.Length - 1)
        {
            index = 0;
        }
        else
        {
            index++;
        }

        _messages.Clear();
        ServerChangeScene(maps[index]);
    }

    public void SetFriendlyFire(bool friendlyFire)
    {
        FriendlyFire = friendlyFire;

        if (!LocalPlayer)
        {
            return;
        }

        LocalPlayer.SetFriendlyFire(friendlyFire);
    }

    public void SetLocalPlayer(PlayerController localPlayer)
    {
        LocalPlayer = localPlayer;
    }
    
    public void SetTeam()
    {
        if (LocalPlayer == null)
        {
            return;
        }

        Team = LocalPlayer.Team;
    }

    public void SetTeamMemberIndex()
    {
        if (LocalPlayer == null)
        {
            return;
        }

        TeamMemberIndex = LocalPlayer.TeamMemberIndex;
    }

    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        base.Awake();

        SetResolution(PlayerPrefs.GetInt(FullscreenKey) != 0, true);

        networkAddress = PlayerPrefs.GetString(nameof(networkAddress), DefaultAddress).Trim();

        PlayerName = PlayerPrefs.GetString(nameof(PlayerName), DefaultPlayerName).Trim();

        _sensitivity = PlayerPrefs.GetFloat(nameof(Sensitivity), DefaultSensitivity);

        Sound = PlayerPrefs.GetFloat(nameof(Sound), DefaultAudio);

        _music = PlayerPrefs.GetFloat(nameof(_music), DefaultAudio);
    }

    public override void Start()
    {
        base.Start();

        _musicSource = GetComponent<AudioSource>();
        _musicSource.volume = _music;
        
        _optionsRoot = GetComponent<UIDocument>().rootVisualElement;
            
        _closeButton = _optionsRoot.Q<Button>("CloseButton");
        _lobbyButton = _optionsRoot.Q<Button>("LobbyButton");
        _leaveButton = _optionsRoot.Q<Button>("LeaveButton");

        _soundSlider = _optionsRoot.Q<Slider>("SoundSlider");
        _musicSlider = _optionsRoot.Q<Slider>("MusicSlider");

        _sensitivityTextField = _optionsRoot.Q<TextField>("SensitivityTextField");
        
        _closeButton.clicked += HideOptions;
        _lobbyButton.clicked += ReturnToLobby;
        _leaveButton.clicked += LeaveMatch;

        _soundSlider.RegisterValueChangedCallback(SoundChanged);
        _soundSlider.value = Sound;
        
        _musicSlider.RegisterValueChangedCallback(MusicChanged);
        _musicSlider.value = _music;

        _sensitivityTextField.RegisterValueChangedCallback(SensitivityChanged);
        _sensitivityTextField.value = _sensitivity.ToString();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (_closeButton != null)
        {
            _closeButton.clicked -= HideOptions;
        }

        if (_lobbyButton != null)
        {
            _lobbyButton.clicked -= ReturnToLobby;
        }

        if (_leaveButton != null)
        {
            _leaveButton.clicked -= LeaveMatch;
        }

        _sensitivityTextField?.UnregisterValueChangedCallback(SensitivityChanged);
        _soundSlider?.UnregisterValueChangedCallback(SoundChanged);
        _musicSlider?.UnregisterValueChangedCallback(MusicChanged);

        SaveResolution();
    }
    
    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        SaveResolution();
    }

    public override void LateUpdate()
    {
        base.LateUpdate();

        HandleCursor();

        HandleOptions();

        HandleMessages();

        if (IsLobby)
        {
            ReadyToStart();
        }

        if (!IsClient)
        {
            return;
        }

        HandleInteractable();
        HandleCamera();
    }
    
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);

        StartCoroutine(ServerReturnToLobby());
    }
    
    private IEnumerator ServerReturnToLobby()
    {
        yield return 0;
        
        if (SceneManager.GetActiveScene().name == onlineScene || networkSceneName == onlineScene)
        {
            yield break;
        }
        
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        if (players.All(p => p.Team != Team.Red) || players.All(p => p.Team != Team.Blue))
        {
            ReturnToLobby();
        }
    }

    private void HandleInteractable()
    {
        if (_optionsOpen || CameraPosition == null)
        {
            return;
        }
        
        InteractableBase interactableBase = FindObjectsOfType<InteractableBase>()
            .Where(i => Vector3.Distance(CameraPosition.position, i.transform.position) <= i.ActivationDistance)
            .OrderBy(i => Vector3.Distance(CameraPosition.position, i.transform.position)).FirstOrDefault();

        if (interactableBase == null)
        {
            DisplayMessage = string.Empty;
            return;
        }

        DisplayMessage = interactableBase.DisplayMessage;
        
        if (!_interact)
        {
            return;
        }

        interactableBase.Interact();
        _interact = false;
    }

    private void HandleCamera()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam is null)
        {
            return;
        }
        
        Transform camTransform = cam.transform;
        
        if (DeathTarget != null)
        {
            camTransform.position = DeathPosition;
            camTransform.LookAt(DeathTarget.CameraPosition);
        }
        else if (CameraPosition != null)
        {
            camTransform.position = CameraPosition.position;
            camTransform.rotation = CameraPosition.rotation;
        }

        transform.position = camTransform.position;
    }

    public void SetPlayerName(string playerName)
    {
        PlayerName = playerName.Trim();
    }

    private void ReadyToStart()
    {
        int total = 0;
        int ready = 0;
        int red = 0;
        int blue = 0;
        
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            switch (player.Team)
            {
                case Team.Red:
                    red++;
                    break;
                case Team.Blue:
                    blue++;
                    break;
            }
        
            total++;
            if (!player.Ready)
            {
                continue;
            }

            ready++;
        }
        
        TotalPlayers = total;
        ReadyPlayers = ready;
        RedPlayers = red;
        BluePlayers = blue;

        int maxPlayers = maxConnections / 2;
        if (IsServer && RedPlayers > 0 && BluePlayers > 0 && RedPlayers <= maxPlayers && BluePlayers <= maxPlayers && RedPlayers + BluePlayers == TotalPlayers && ReadyPlayers == TotalPlayers)
        {
            _startMapCoroutine ??= StartCoroutine(LoadMapCountdown());
        }
        else if (_startMapCoroutine != null)
        {
            StopCoroutine(_startMapCoroutine);
            _startMapCoroutine = null;
        }
    }

    private IEnumerator LoadMapCountdown()
    {
        yield return new WaitForSeconds(SceneSwitchDelay);
        LoadMap();
    }

    private void ReturnToLobby()
    {
        if (!IsServer)
        {
            return;
        }
        
        ServerChangeScene(onlineScene);
        _optionsOpen = false;
    }
    
    private void SensitivityChanged(ChangeEvent<string> evt)
    {
        string sensitivityString = new string(evt.newValue.Trim().ToCharArray().Where(c => char.IsDigit(c) || c == '.').ToArray());
        bool containsExcessDecimal = false;
        while (sensitivityString.Contains(".."))
        {
            containsExcessDecimal = true;
            sensitivityString = sensitivityString.Replace("..", ".");
        }

        if (containsExcessDecimal || !float.TryParse(sensitivityString, out float sensitivity) || sensitivityString != evt.newValue && sensitivityString[sensitivityString.Length - 1] != '.')
        {
            _sensitivityTextField.value = sensitivityString;
            return;
        }

        _sensitivity = Mathf.Max(Math.Abs(sensitivity), 0.01f);
        
        PlayerPrefs.SetFloat(nameof(Sensitivity), _sensitivity);
    }

    private void SoundChanged(ChangeEvent<float> evt)
    {
        Sound = evt.newValue;
        foreach (AudioSource audioSource in FindObjectsOfType<AudioSource>().Where(a => a != _musicSource))
        {
            audioSource.volume = Sound;
        }
        PlayerPrefs.SetFloat(nameof(Sound), Sound);
    }

    private void MusicChanged(ChangeEvent<float> evt)
    {
        _music = evt.newValue;
        _musicSource.volume = _music;
        PlayerPrefs.SetFloat(nameof(_music), _music);
    }

    private void HideOptions()
    {
        _optionsOpen = false;
    }

    private void HandleCursor()
    {
        bool visible = !IsServer && !IsClient || _optionsOpen;
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void HandleOptions()
    {
        foreach (VisualElement root in OtherUIDocuments.Select(otherUIDocument => otherUIDocument.rootVisualElement))
        {
            root.visible = !_optionsOpen;
            root.style.display = !_optionsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            root.SetEnabled(!_optionsOpen);
        }
        
        _optionsRoot.visible = _optionsOpen;
        _optionsRoot.SetEnabled(_optionsOpen);
        _optionsRoot.style.display = _optionsOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (_optionsOpen)
        {
            bool visible = !IsLobby && IsServer;

            _lobbyButton.visible = visible;
            _lobbyButton.SetEnabled(visible);
            _lobbyButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            visible = IsServer || IsClient;

            _leaveButton.visible = visible;
            _leaveButton.SetEnabled(visible);
            _leaveButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!_optionsOpen && string.IsNullOrWhiteSpace(_sensitivityTextField.value))
        {
            _sensitivityTextField.value = 1.ToString();
        }
    }

    private void HandleMessages()
    {
        for (int index = 0; index < _messages.Count; index++)
        {
            Message message = _messages[index];
            message.time -= Time.deltaTime;
            _messages[index] = message;
        }

        _messages = _messages.GroupBy(m => m.text).Select(m => m.First()).Where(m => m.time > 0).OrderByDescending(m => m.time).Take(NumberMessages).ToList();
    }

    private void LeaveMatch()
    {
        switch (mode)
        {
            case NetworkManagerMode.Offline:
                try
                {
                    StopClient();
                }
                catch { }
                break;
            case NetworkManagerMode.ServerOnly:
                StopServer();
                Application.Quit();
                break;
            case NetworkManagerMode.ClientOnly:
                StopClient();
                break;
            case NetworkManagerMode.Host:
                StopHost();
                break;
        }
        
        Team = Team.None;
        _optionsOpen = false;
    }

    private static void SetResolution(bool fullscreen, bool initial = false)
    {
        int width = PlayerPrefs.GetInt(WidthKey);
        int height = PlayerPrefs.GetInt(HeightKey);
        
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);

        if (fullscreen)
        {
            if (!initial)
            {
                PlayerPrefs.SetInt(WidthKey, Screen.width);
                PlayerPrefs.SetInt(HeightKey, Screen.height);
            }
            
            Resolution resolution = Screen.resolutions.OrderByDescending(r => r.width * r.height).FirstOrDefault();
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.ExclusiveFullScreen, 0);

            return;
        }

        if (width <= 0)
        {
            width = DefaultResolution;
        }

        if (height <= 0)
        {
            height = DefaultResolution;
        }
            
        Screen.SetResolution(width, height, FullScreenMode.Windowed, 0);
    }

    private static void SaveResolution()
    {
        PlayerPrefs.SetInt(FullscreenKey, Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1);

        if (Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            return;
        }
        
        PlayerPrefs.SetInt(WidthKey, Screen.width);
        PlayerPrefs.SetInt(HeightKey, Screen.height);
    }

    private void OnMove(InputValue value)
    {
        _move = value.Get<Vector2>();
    }

    private void OnLook(InputValue value)
    {
        _look = value.Get<Vector2>();
    }
    
    private void OnJump(InputValue value)
    {
        _jump = value.isPressed;
    }
    
    private void OnShoot(InputValue value)
    {
        _shoot = value.isPressed;
    }
    
    private void OnInteract(InputValue value)
    {
        _interact = value.isPressed;
    }

    private void OnEscape(InputValue value)
    {
        _optionsOpen = !_optionsOpen;
    }

    private void OnFullscreen(InputValue value)
    {
        SetResolution(Screen.fullScreenMode == FullScreenMode.Windowed);
    }

    private void OnWeapon1(InputValue value)
    {
        SwitchWeapon(0);
    }

    private void OnWeapon2(InputValue value)
    {
        SwitchWeapon(1);
    }

    private void OnWeapon3(InputValue value)
    {
        SwitchWeapon(2);
    }

    private void OnWeapon4(InputValue value)
    {
        SwitchWeapon(3);
    }

    private void OnWeapon5(InputValue value)
    {
        SwitchWeapon(4);
    }

    private void OnWeapon6(InputValue value)
    {
        SwitchWeapon(5);
    }

    private void OnWeapon7(InputValue value)
    {
        SwitchWeapon(6);
    }

    private void OnWeapon8(InputValue value)
    {
        SwitchWeapon(7);
    }

    private void OnWeapon9(InputValue value)
    {
        SwitchWeapon(8);
    }

    private void OnWeapon10(InputValue value)
    {
        SwitchWeapon(9);
    }

    private void OnSwapWeapon(InputValue value)
    {
        if (LocalPlayer == null || _optionsOpen)
        {
            return;
        }

        LocalPlayer.SwapWeapon((int) value.Get<float>());
    }

    private void SwitchWeapon(int weaponIndex)
    {
        if (LocalPlayer == null || _optionsOpen)
        {
            return;
        }

        LocalPlayer.SwitchWeapon(weaponIndex);
    }
}