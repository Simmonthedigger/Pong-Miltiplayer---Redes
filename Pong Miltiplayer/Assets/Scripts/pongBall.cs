using UnityEngine;
using TMPro;
using System.Globalization;
using System.Collections;

public class PongBallPhysics : MonoBehaviour
{
    public UdpClient4Players networkClient;
    public float ballSpeed = 8f;

    [Header("Interface do Placar")]
    public TextMeshProUGUI scoreTextP1;
    public TextMeshProUGUI scoreTextP2;
    public TextMeshProUGUI winText;

    [Header("Placar Interno")]
    public int scorePlayer1 = 0;
    public int scorePlayer2 = 0;
    public int maxScore = 10;

    private Rigidbody2D rb;
    private Vector3 remoteBallPos = Vector3.zero;

    private bool isBallInPlay = false;
    private bool allPlayersConnected = false;
    private bool isGameOver = false;

    // 1 = Lança para a Direita, 2 = Lança para a Esquerda
    private int lastScoredSide = 1;

    private bool pendingScoreUpdate = false;
    private bool isRespawning = false;

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
        // Atualiza placar vindo da rede (em clientes secundários)
        if (pendingScoreUpdate)
        {
            pendingScoreUpdate = false;
            isBallInPlay = false;
            transform.position = Vector3.zero;
            UpdateUI();
            CheckWinCondition();
        }

        if (networkClient == null || networkClient.myId == -1) return;

        // Gestão de Reinício de Jogo
        if (isGameOver)
        {
            if (networkClient.myId == 1 && Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
            else if (networkClient.myId != 1 && Input.GetKeyDown(KeyCode.R))
            {
                networkClient.SendNetworkMessage("REQUEST_RESTART");
            }
            return;
        }

        // --- PLAYER 1: Autoridade da Física ---
        if (networkClient.myId == 1)
        {
            if (!rb.simulated) rb.simulated = true;

            // Inicia automaticamente quando os 4 jogadores se conectam
            if (allPlayersConnected && !isBallInPlay && !isRespawning)
            {
                StartCoroutine(AutoLaunchRoutine(1.0f));
            }

            if (isBallInPlay && rb.linearVelocity.sqrMagnitude > 0)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * ballSpeed;
            }

            SendBallPosition();
        }
        // --- PLAYERS 2, 3 e 4: Seguidores de Rede ---
        else
        {
            if (rb.simulated) rb.simulated = false;

            transform.position = Vector3.Lerp(transform.position, remoteBallPos, Time.deltaTime * 25f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (networkClient.myId != 1 || !isBallInPlay || isGameOver) return;

        // Se marcou no golo esquerdo (a bola ia para a esquerda), sai em direção ao lado direito (dirX = 1)
        if (collision.CompareTag("GoalLeft"))
        {
            scorePlayer2++;
            lastScoredSide = 1; // Próximo lançamento vai para a DIREITA
            ResetToCenterAndRelaunch();
        }
        // Se marcou no golo direito (a bola ia para a direita), sai em direção ao lado esquerdo (dirX = -1)
        else if (collision.CompareTag("GoalRight"))
        {
            scorePlayer1++;
            lastScoredSide = 2; // Próximo lançamento vai para a ESQUERDA
            ResetToCenterAndRelaunch();
        }

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (scorePlayer1 >= maxScore || scorePlayer2 >= maxScore)
        {
            isGameOver = true;
            isBallInPlay = false;
            StopAllCoroutines();
            transform.position = Vector3.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            string winnerMsg = scorePlayer1 >= maxScore ? "TIME 1 VENCEU!" : "TIME 2 VENCEU!";

            if (winText != null)
            {
                winText.text = winnerMsg + "\nPressione 'R' para reiniciar";
                winText.gameObject.SetActive(true);
            }
        }
    }

    public void SetAllPlayersConnected()
    {
        allPlayersConnected = true;
    }

    // Método mantido para compatibilidade com o pacote REQUEST_LAUNCH da rede
    public void RemoteRequestLaunch()
    {
        if (networkClient.myId == 1 && !isBallInPlay && !isGameOver)
        {
            LaunchBall();
        }
    }

    private void LaunchBall()
    {
        isBallInPlay = true;

        // Lança para o lado oposto ao que sofreu o golo
        float dirX = (lastScoredSide == 1) ? 1f : -1f;
        float dirY = Random.Range(-0.5f, 0.5f);
        if (Mathf.Abs(dirY) < 0.2f) dirY = 0.3f;

        Vector2 direction = new Vector2(dirX, dirY).normalized;
        rb.linearVelocity = direction * ballSpeed;
    }

    private IEnumerator AutoLaunchRoutine(float delay)
    {
        isRespawning = true;
        yield return new WaitForSeconds(delay);

        if (!isGameOver)
        {
            LaunchBall();
        }
        isRespawning = false;
    }

    private void ResetToCenterAndRelaunch()
    {
        isBallInPlay = false;
        transform.position = Vector3.zero;
        rb.linearVelocity = Vector2.zero;

        UpdateUI();

        if (networkClient != null)
        {
            string scoreMsg = $"SCORE:{scorePlayer1};{scorePlayer2};{lastScoredSide}";
            networkClient.SendNetworkMessage(scoreMsg);
        }

        // Aguarda 1.5 segundos antes de lançar a bola automaticamente
        StartCoroutine(AutoLaunchRoutine(1.5f));
    }

    public void RestartGame()
    {
        StopAllCoroutines();
        scorePlayer1 = 0;
        scorePlayer2 = 0;
        isGameOver = false;
        isBallInPlay = false;
        isRespawning = false;
        lastScoredSide = 1;
        transform.position = Vector3.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (winText != null) winText.gameObject.SetActive(false);
        UpdateUI();

        if (networkClient != null)
        {
            string scoreMsg = $"SCORE:0;0;1";
            networkClient.SendNetworkMessage(scoreMsg);
        }

        // Relança a bola após reiniciar
        if (networkClient.myId == 1 && allPlayersConnected)
        {
            StartCoroutine(AutoLaunchRoutine(1.5f));
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

    public void UpdateScore(int p1, int p2, int nextSide)
    {
        scorePlayer1 = p1;
        scorePlayer2 = p2;
        lastScoredSide = nextSide;

        pendingScoreUpdate = true;
    }

    private void UpdateUI()
    {
        if (scoreTextP1 != null) scoreTextP1.text = scorePlayer1.ToString();
        if (scoreTextP2 != null) scoreTextP2.text = scorePlayer2.ToString();
    }
}