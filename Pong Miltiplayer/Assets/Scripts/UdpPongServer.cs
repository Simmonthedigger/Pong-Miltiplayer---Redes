using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;

public class UdpPongServer : MonoBehaviour
{
    private UdpClient server;
    private IPEndPoint anyEP;
    private Thread receiveThread;

    // Armazena conexões: Key = IP:Porta, Value = PlayerID (1 ou 2)
    private Dictionary<string, int> clientIds = new Dictionary<string, int>();
    private Dictionary<int, IPEndPoint> clientEndpoints = new Dictionary<int, IPEndPoint>();
    private int nextId = 1;

    // Estado do Jogo (Física da Bola)
    private Vector2 ballPos = Vector2.zero;
    private Vector2 ballVelocity;
    public float ballSpeed = 8f;

    // Posições dos Jogadores
    private float p1Y = 0f;
    private float p2Y = 0f;

    // Placar
    private int scoreP1 = 0;
    private int scoreP2 = 0;
    public int maxScore = 5;
    private bool gameActive = false;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        ResetBall();
    }

    void Update()
    {
        if (!gameActive && clientIds.Count >= 2)
        {
            gameActive = true;
            ResetBall();
        }

        if (gameActive)
        {
            MoveBall();
            BroadcastGameState();
        }
    }

    private void MoveBall()
    {
        ballPos += ballVelocity * Time.deltaTime;

        // Colisão com teto e chão (Limites Y: -4.5 a 4.5)
        if (ballPos.y >= 4.5f || ballPos.y <= -4.5f)
        {
            ballVelocity.y *= -1f;
        }

        // Colisão com Player 1 (X: -7.5)
        if (ballPos.x <= -7.3f && ballPos.x >= -7.7f && Mathf.Abs(ballPos.y - p1Y) <= 1.2f)
        {
            ballVelocity.x = Mathf.Abs(ballVelocity.x);
        }

        // Colisão com Player 2 (X: 7.5)
        if (ballPos.x >= 7.3f && ballPos.x <= 7.7f && Mathf.Abs(ballPos.y - p2Y) <= 1.2f)
        {
            ballVelocity.x = -Mathf.Abs(ballVelocity.x);
        }

        // Pontuação (Limites X: -9 a 9)
        if (ballPos.x < -9f)
        {
            scoreP2++;
            CheckWinCondition();
        }
        else if (ballPos.x > 9f)
        {
            scoreP1++;
            CheckWinCondition();
        }
    }

    private void ResetBall()
    {
        ballPos = Vector2.zero;
        float dirX = Random.Range(0, 2) == 0 ? -1f : 1f;
        float dirY = Random.Range(-0.5f, 0.5f);
        ballVelocity = new Vector2(dirX, dirY).normalized * ballSpeed;
    }

    private void CheckWinCondition()
    {
        if (scoreP1 >= maxScore || scoreP2 >= maxScore)
        {
            int winner = scoreP1 >= maxScore ? 1 : 2;
            BroadcastMessage($"WIN:{winner}");
            gameActive = false;
        }
        else
        {
            ResetBall();
        }
    }

    private void ReceiveData()
    {
        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = anyEP.Address + ":" + anyEP.Port;

                if (!clientIds.ContainsKey(key) && clientIds.Count < 2)
                {
                    int assignedId = nextId++;
                    clientIds[key] = assignedId;
                    clientEndpoints[assignedId] = new IPEndPoint(anyEP.Address, anyEP.Port);

                    string assignMsg = "ASSIGN:" + assignedId;
                    byte[] aData = Encoding.UTF8.GetBytes(assignMsg);
                    server.Send(aData, aData.Length, anyEP);
                }

                if (msg.StartsWith("POS:"))
                {
                    float y = float.Parse(msg.Substring(4), CultureInfo.InvariantCulture);
                    int id = clientIds[key];
                    if (id == 1) p1Y = y;
                    else if (id == 2) p2Y = y;
                }
                else if (msg == "RESTART")
                {
                    scoreP1 = 0;
                    scoreP2 = 0;
                    gameActive = true;
                    ResetBall();
                }
            }
            catch (System.Exception) { break; }
        }
    }

    private void BroadcastGameState()
    {
        // Formato: STATE:BallX;BallY;P1Y;P2Y;Score1;Score2
        string dataStr = string.Format(CultureInfo.InvariantCulture,
            "STATE:{0:F2};{1:F2};{2:F2};{3:F2};{4};{5}",
            ballPos.x, ballPos.y, p1Y, p2Y, scoreP1, scoreP2);

        BroadcastMessage(dataStr);
    }

    private void BroadcastMessage(string msg)
    {
        byte[] bdata = Encoding.UTF8.GetBytes(msg);
        foreach (var ep in clientEndpoints.Values)
        {
            server.Send(bdata, bdata.Length, ep);
        }
    }

    private void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (server != null) server.Close();
    }
}

public class PersistentObject1 : MonoBehaviour
{
    private static PersistentObject1 instance;

    void Awake()
    {
        // Se já existir uma instância deste objeto, destrói a nova duplicata
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}