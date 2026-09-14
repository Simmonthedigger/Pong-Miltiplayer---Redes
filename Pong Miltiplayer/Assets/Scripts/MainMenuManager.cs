using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Suporte para TextMeshPro

public class MainMenuManager : MonoBehaviour
{
    // Campos usando TextMeshPro (compatível com os elementos de UI padrão das versões recentes do Unity)
    public TMP_InputField ipInputField;
    public TextMeshProUGUI statusText;
    public Button playButton;

    void Start()
    {
        if (playButton != null)
        {
            playButton.interactable = false;
        }

        if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
        {
            ipInputField.text = "127.0.0.1";
        }
    }

    public void OnStartServerClicked()
    {
        // Instancia o servidor na cena se ele ainda não existir usando o método atualizado
        if (Object.FindAnyObjectByType<UdpPongServer>() == null)
        {
            GameObject serverObj = new GameObject("UdpPongServer");
            serverObj.AddComponent<UdpPongServer>();
            DontDestroyOnLoad(serverObj);
        }

        if (statusText != null)
        {
            statusText.text = "Servidor Rodando na Porta 5001...";
        }
    }

    public void OnConnectButtonClicked()
    {
        string ip = (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
            ? ipInputField.text
            : "127.0.0.1";

        if (statusText != null)
        {
            statusText.text = "Conectando ao IP " + ip + "...";
        }

        if (UdpPongClient.Instance != null)
        {
            UdpPongClient.Instance.ConnectToServer(ip);
        }
        else
        {
            Debug.LogError("UdpPongClient não foi encontrado na cena!");
        }
    }

    void Update()
    {
        // Verifica se o singleton existe e se o cliente já está conectado
        if (UdpPongClient.Instance != null && UdpPongClient.Instance.isConnected)
        {
            if (statusText != null)
            {
                statusText.text = "Conectado! Player ID: " + UdpPongClient.Instance.myId;
            }

            if (playButton != null)
            {
                playButton.interactable = true;
            }
        }
    }

    public void OnPlayButtonClicked()
    {
        SceneManager.LoadScene("GameplayScene");
        SceneManager.LoadScene("UIScene", LoadSceneMode.Additive);
    }
}