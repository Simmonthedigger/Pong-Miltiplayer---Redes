using UnityEngine;
using TMPro;
using System.Globalization;

public class PongBallPhysics : MonoBehaviour
{
    public UdpClientTwoClients networkClient;
    public float ballSpeed = 8f;

    [Header("Interface do Placar")]
    public TextMeshProUGUI scoreTextP1;
    public TextMeshProUGUI scoreTextP2;
    public TextMeshProUGUI winText; // Novo texto para exibir a mensagem de vitória

    [Header("Placar Interno")]
    public int scorePlayer1 = 0;
    public int scorePlayer2 = 0;
    public int maxScore = 10; // Condição de vitória

    private Rigidbody2D rb;
    private Vector3 remoteBallPos = Vector3.zero;

    private bool isBallInPlay = false;
    private bool player2Connected = false;
    private bool isGameOver = false; // Controla se o jogo terminou
    private int servingPlayerId = 1;

    private bool pendingScoreUpdate = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        UpdateUI();
        if (winText != null) winText.gameObject.SetActive(false);
    }

    void Update()
    {
        // Atualiza placar na Main Thread vindo da rede
        if (pendingScoreUpdate)
        {
            pendingScoreUpdate = false;
            isBallInPlay = false;
            transform.position = Vector3.zero;
            UpdateUI();
            CheckWinCondition();
        }

        if (networkClient == null || networkClient.myId == -1) return;

        // Se o jogo acabou, permite reiniciar ao apertar 'R' (apenas Player 1 executa o reset global)
        if (isGameOver)
        {
            if (networkClient.myId == 1 && Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
            else if (networkClient.myId == 2 && Input.GetKeyDown(KeyCode.R))
            {
                networkClient.SendNetworkMessage("REQUEST_RESTART");
            }
            return;
        }

        // --- PLAYER 1: Autoridade de Física ---
        if (networkClient.myId == 1)
        {
            if (!rb.simulated) rb.simulated = true;

            if (!isBallInPlay)
            {
                transform.position = Vector3.zero;
                rb.linearVelocity = Vector2.zero;

                if (player2Connected && servingPlayerId == 1 && Input.GetKeyDown(KeyCode.Space))
                {
                    LaunchBall();
                }
            }
            else
            {
                if (rb.linearVelocity.sqrMagnitude > 0)
                {
                    rb.linearVelocity = rb.linearVelocity.normalized * ballSpeed;
                }
            }

            SendBallPosition();
        }
        // --- PLAYER 2: Seguidor de Rede ---
        else
        {
            if (rb.simulated) rb.simulated = false;

            if (!isBallInPlay && servingPlayerId == 2 && Input.GetKeyDown(KeyCode.Space))
            {
                networkClient.SendNetworkMessage("REQUEST_LAUNCH");
            }

            transform.position = Vector3.Lerp(transform.position, remoteBallPos, Time.deltaTime * 25f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (networkClient.myId != 1 || !isBallInPlay || isGameOver) return;

        if (collision.CompareTag("GoalLeft"))
        {
            scorePlayer2++;
            servingPlayerId = 1;
            ResetToCenter();
        }
        else if (collision.CompareTag("GoalRight"))
        {
            scorePlayer1++;
            servingPlayerId = 2;
            ResetToCenter();
        }

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (scorePlayer1 >= maxScore || scorePlayer2 >= maxScore)
        {
            isGameOver = true;
            isBallInPlay = false;
            transform.position = Vector3.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            string winnerMsg = scorePlayer1 >= maxScore ? "PLAYER 1 VENCEU!" : "PLAYER 2 VENCEU!";

            if (winText != null)
            {
                winText.text = winnerMsg + "\nPressione 'R' para reiniciar";
                winText.gameObject.SetActive(true);
            }
        }
    }

    public void SetPlayer2Connected()
    {
        player2Connected = true;
    }

    public void RemoteRequestLaunch()
    {
        if (networkClient.myId == 1 && !isBallInPlay && servingPlayerId == 2 && !isGameOver)
        {
            LaunchBall();
        }
    }

    private void LaunchBall()
    {
        isBallInPlay = true;

        float dirX = servingPlayerId == 1 ? -1f : 1f;
        float dirY = Random.Range(-0.5f, 0.5f);
        if (Mathf.Abs(dirY) < 0.2f) dirY = 0.3f;

        Vector2 direction = new Vector2(dirX, dirY).normalized;
        rb.linearVelocity = direction * ballSpeed;
    }

    private void ResetToCenter()
    {
        isBallInPlay = false;
        transform.position = Vector3.zero;
        rb.linearVelocity = Vector2.zero;

        UpdateUI();

        if (networkClient != null)
        {
            string scoreMsg = $"SCORE:{scorePlayer1};{scorePlayer2};{servingPlayerId}";
            networkClient.SendNetworkMessage(scoreMsg);
        }
    }

    public void RestartGame()
    {
        scorePlayer1 = 0;
        scorePlayer2 = 0;
        isGameOver = false;
        isBallInPlay = false;
        servingPlayerId = 1;
        transform.position = Vector3.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (winText != null) winText.gameObject.SetActive(false);
        UpdateUI();

        if (networkClient != null)
        {
            string scoreMsg = $"SCORE:0;0;1";
            networkClient.SendNetworkMessage(scoreMsg);
        }
    }

    private void SendBallPosition()
    {
        if (networkClient != null)
        {
            string msg = "BALL:" +
                transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

            networkClient.SendNetworkMessage(msg);
        }
    }

    public void UpdateRemotePosition(float x, float y)
    {
        remoteBallPos = new Vector3(x, y, 0);
    }

    public void UpdateScore(int p1, int p2, int nextServer)
    {
        scorePlayer1 = p1;
        scorePlayer2 = p2;
        servingPlayerId = nextServer;

        pendingScoreUpdate = true;
    }

    private void UpdateUI()
    {
        if (scoreTextP1 != null) scoreTextP1.text = scorePlayer1.ToString();
        if (scoreTextP2 != null) scoreTextP2.text = scorePlayer2.ToString();
    }
}