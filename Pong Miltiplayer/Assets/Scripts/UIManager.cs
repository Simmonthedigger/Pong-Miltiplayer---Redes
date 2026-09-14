using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Text scoreTextP1;
    public Text scoreTextP2;
    public GameObject restartPanel;
    public Text winText;

    void Start()
    {
        if (restartPanel != null)
        {
            restartPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!UdpPongClient.Instance.isConnected) return;

        // Atualiza o Placar
        scoreTextP1.text = UdpPongClient.Instance.scoreP1.ToString();
        scoreTextP2.text = UdpPongClient.Instance.scoreP2.ToString();

        // Checa Condição de Vitória
        if (UdpPongClient.Instance.winnerId != -1)
        {
            restartPanel.SetActive(true);
            winText.text = "PLAYER " + UdpPongClient.Instance.winnerId + " VENCEU!";
        }
        else
        {
            restartPanel.SetActive(false);
        }
    }

    public void OnRestartButtonClicked()
    {
        UdpPongClient.Instance.winnerId = -1;
        UdpPongClient.Instance.SendRestart();
        restartPanel.SetActive(false);
    }
}