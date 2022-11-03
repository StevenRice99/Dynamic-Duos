using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MenuUI : MonoBehaviour
    {
        private Button _hostButton;
        
        private Button _joinButton;
        
        private Button _quitButton;

        private TextField _nameTextField;

        private TextField _addressTextField;

        private void Start()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            
            _hostButton = root.Q<Button>("HostButton");
            _joinButton = root.Q<Button>("JoinButton");
            _quitButton = root.Q<Button>("QuitButton");

            _nameTextField = root.Q<TextField>("NameTextField");
            _addressTextField = root.Q<TextField>("AddressTextField");

            _hostButton.clicked += NetworkManager.singleton.StartHost;
            _joinButton.clicked += NetworkManager.singleton.StartClient;
            _quitButton.clicked += Application.Quit;

            _nameTextField.value = GameManager.Instance.PlayerName;
            _addressTextField.value = NetworkManager.singleton.networkAddress;

            _nameTextField.RegisterValueChangedCallback(NameChanged);
            _addressTextField.RegisterValueChangedCallback(AddressChanged);
        }

        private void OnDestroy()
        {
            try
            {
                _hostButton.clicked -= NetworkManager.singleton.StartHost;
                _joinButton.clicked -= NetworkManager.singleton.StartClient;
                _quitButton.clicked -= Application.Quit;

                _nameTextField.UnregisterValueChangedCallback(NameChanged);
                _addressTextField.UnregisterValueChangedCallback(AddressChanged);
            }
            catch { }
        }

        private void NameChanged(ChangeEvent<string> evt)
        {
            if (string.IsNullOrWhiteSpace(evt.newValue))
            {
                GameManager.Instance.SetPlayerName(GameManager.DefaultPlayerName);
                return;
            }

            string playerName = new string(evt.newValue.Trim().ToCharArray().Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (playerName != evt.newValue)
            {
                _nameTextField.value = playerName;
            }
            
            GameManager.Instance.SetPlayerName(evt.newValue);
            PlayerPrefs.SetString(nameof(GameManager.Instance.PlayerName), playerName);
        }

        private void AddressChanged(ChangeEvent<string> evt)
        {
            if (string.IsNullOrWhiteSpace(evt.newValue))
            {
                NetworkManager.singleton.networkAddress = GameManager.DefaultAddress;
                return;
            }

            string networkAddress = new string(evt.newValue.Trim().ToCharArray().Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (networkAddress != evt.newValue)
            {
                _addressTextField.value = networkAddress;
            }
            
            NetworkManager.singleton.networkAddress = networkAddress;
            PlayerPrefs.SetString(nameof(NetworkManager.singleton.networkAddress), networkAddress);
        }
    }
}