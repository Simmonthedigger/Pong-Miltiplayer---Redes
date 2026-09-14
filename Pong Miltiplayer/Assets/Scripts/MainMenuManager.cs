using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public InputField ipInputField;
    public Text statusText;
    public Button playButton;

    void Start()
    {
        playButton.interactable = false;
        if (ipInputField != null) ipInputField.text = "127.0.0.1";
    }

    public void OnConnectButtonClicked()
    {
        string ip = ipInputField.text;
        statusText.text = "Conectando...";
        UdpPongClient.Instance.ConnectToServer(ip);
    }

    void Update()
    {
        if (UdpPongClient.Instance.isConnected)
        {
            statusText.text = "Conectado! Player ID: " + UdpPongClient.Instance.myId;
            playButton.interactable = true;
        }
    }

    public void OnPlayButtonClicked()
    {
        // Carrega a cena de Gameplay e a cena de UI de forma aditiva
        SceneManager.LoadScene("GameplayScene");
        SceneManager.LoadScene("UIScene", LoadSceneMode.Additive);
    }
}