using UnityEngine;
using TMPro; // Necessário para a Interface de Texto (TextMeshPro)
using System.Globalization;

public class PongBallPhysics : MonoBehaviour
{
    public UdpClientTwoClients networkClient;
    public float ballSpeed = 8f;

    [Header("Interface do Placar")]
    public TextMeshProUGUI scoreTextP1;
    public TextMeshProUGUI scoreTextP2;

    [Header("Placar Interno")]
    public int scorePlayer1 = 0;
    public int scorePlayer2 = 0;

    private Rigidbody2D rb;
    private Vector3 remoteBallPos = Vector3.zero;

    private bool isBallInPlay = false;
    private bool player2Connected = false;
    private int servingPlayerId = 1; // 1 = Player 1 saca, 2 = Player 2 saca

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        UpdateUI();
    }

    void Update()
    {
        if (networkClient == null || networkClient.myId == -1) return;

        // --- PLAYER 1: Autoridade na Física ---
        if (networkClient.myId == 1)
        {
            if (!rb.simulated) rb.simulated = true;

            if (!isBallInPlay)
            {
                transform.position = Vector3.zero;
                rb.linearVelocity = Vector2.zero;

                // Se for a vez do Player 1 sacar e ele apertar Espaço
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

            // Se for a vez do Player 2 sacar e ele apertar Espaço
            if (!isBallInPlay && servingPlayerId == 2 && Input.GetKeyDown(KeyCode.Space))
            {
                // Envia o pedido de lançamento para o Player 1
                networkClient.SendNetworkMessage("REQUEST_LAUNCH");
            }

            transform.position = Vector3.Lerp(transform.position, remoteBallPos, Time.deltaTime * 25f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Apenas o Player 1 detecta as colisões dos pontos
        if (networkClient.myId != 1 || !isBallInPlay) return;

        if (collision.CompareTag("GoalLeft"))
        {
            scorePlayer2++;
            servingPlayerId = 1; // Quem sofreu o ponto saca
            ResetToCenter();
        }
        else if (collision.CompareTag("GoalRight"))
        {
            scorePlayer1++;
            servingPlayerId = 2; // Quem sofreu o ponto saca
            ResetToCenter();
        }
    }

    public void SetPlayer2Connected()
    {
        player2Connected = true;
    }

    public void RemoteRequestLaunch()
    {
        if (networkClient.myId == 1 && !isBallInPlay && servingPlayerId == 2)
        {
            LaunchBall();
        }
    }

    private void LaunchBall()
    {
        isBallInPlay = true;

        // Saque vai em direção a quem precisa defender
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

        // Envia atualização de placar e novo sacador para a rede
        if (networkClient != null)
        {
            string scoreMsg = $"SCORE:{scorePlayer1};{scorePlayer2};{servingPlayerId}";
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
        isBallInPlay = false;
        transform.position = Vector3.zero;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreTextP1 != null) scoreTextP1.text = scorePlayer1.ToString();
        if (scoreTextP2 != null) scoreTextP2.text = scorePlayer2.ToString();
    }
}