using UnityEngine;
using TMPro; // Biblioteca necessária para TextMeshPro

public class UIManager : MonoBehaviour
{
    [Header("UI Placar")]
    public TextMeshProUGUI scoreTextP1;
    public TextMeshProUGUI scoreTextP2;

    [Header("Painel de Vitória")]
    public GameObject restartPanel;
    public TextMeshProUGUI winText;

    void Start()
    {
        if (restartPanel != null)
        {
            restartPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Garante que a instância do cliente existe e está conectada
        if (UdpPongClient.Instance == null || !UdpPongClient.Instance.isConnected) return;

        // Atualiza o Placar
        if (scoreTextP1 != null) scoreTextP1.text = UdpPongClient.Instance.scoreP1.ToString();
        if (scoreTextP2 != null) scoreTextP2.text = UdpPongClient.Instance.scoreP2.ToString();

        // Checa Condição de Vitória
        if (UdpPongClient.Instance.winnerId != -1)
        {
            if (restartPanel != null && !restartPanel.activeSelf)
            {
                restartPanel.SetActive(true);
            }

            if (winText != null)
            {
                winText.text = "PLAYER " + UdpPongClient.Instance.winnerId + " VENCEU!";
            }
        }
        else
        {
            if (restartPanel != null && restartPanel.activeSelf)
            {
                restartPanel.SetActive(false);
            }
        }
    }

    public void OnRestartButtonClicked()
    {
        if (UdpPongClient.Instance != null)
        {
            UdpPongClient.Instance.winnerId = -1;
            UdpPongClient.Instance.SendRestart();
        }

        if (restartPanel != null)
        {
            restartPanel.SetActive(false);
        }
    }
}