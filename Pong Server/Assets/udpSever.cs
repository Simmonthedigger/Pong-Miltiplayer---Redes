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
    private Thread receiveThread;
    private readonly object lockObject = new object();

    // Armazena conexões
    private Dictionary<string, int> clientIds = new Dictionary<string, int>();
    private Dictionary<int, IPEndPoint> clientEndpoints = new Dictionary<int, IPEndPoint>();
    private int nextId = 1;

    // Estado do Jogo
    private Vector2 ballPos = Vector2.zero;
    private Vector2 ballVelocity;
    public float ballSpeed = 8f;

    private float p1Y = 0f;
    private float p2Y = 0f;

    private int scoreP1 = 0;
    private int scoreP2 = 0;
    public int maxScore = 5;
    private bool gameActive = false;

    void Start()
    {
        // Garante taxa de atualização constante mesmo sem renderização gráfica completa
        Application.targetFrameRate = 60;

        server = new UdpClient(5001);

        receiveThread = new Thread(ReceiveData) { IsBackground = true };
        receiveThread.Start();

        ResetBall();
    }

    void Update()
    {
        lock (lockObject)
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
    }

    private void MoveBall()
    {
        ballPos += ballVelocity * Time.deltaTime;

        // Colisão com teto e chão
        if (ballPos.y >= 4.5f || ballPos.y <= -4.5f)
        {
            ballVelocity.y *= -1f;
        }

        // Colisão com Raquetes
        if (ballPos.x <= -7.3f && ballPos.x >= -7.7f && Mathf.Abs(ballPos.y - p1Y) <= 1.2f)
        {
            ballVelocity.x = Mathf.Abs(ballVelocity.x);
        }

        if (ballPos.x >= 7.3f && ballPos.x <= 7.7f && Mathf.Abs(ballPos.y - p2Y) <= 1.2f)
        {
            ballVelocity.x = -Mathf.Abs(ballVelocity.x);
        }

        // Pontuação
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
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = remoteEP.Address + ":" + remoteEP.Port;

                lock (lockObject)
                {
                    if (!clientIds.ContainsKey(key) && clientIds.Count < 2)
                    {
                        int assignedId = nextId++;
                        clientIds[key] = assignedId;
                        clientEndpoints[assignedId] = new IPEndPoint(remoteEP.Address, remoteEP.Port);

                        string assignMsg = "ASSIGN:" + assignedId;
                        byte[] aData = Encoding.UTF8.GetBytes(assignMsg);
                        server.Send(aData, aData.Length, remoteEP);
                    }

                    if (msg.StartsWith("POS:"))
                    {
                        if (clientIds.TryGetValue(key, out int id))
                        {
                            float y = float.Parse(msg.Substring(4), CultureInfo.InvariantCulture);
                            if (id == 1) p1Y = y;
                            else if (id == 2) p2Y = y;
                        }
                    }
                    else if (msg == "RESTART")
                    {
                        scoreP1 = 0;
                        scoreP2 = 0;
                        gameActive = true;
                        ResetBall();
                    }
                }
            }
            catch (SocketException) { break; }
            catch (System.Exception) { /* Ignora leituras corrompidas */ }
        }
    }

    private void BroadcastGameState()
    {
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

    private void OnDestroy()
    {
        CloseSocket();
    }

    private void OnApplicationQuit()
    {
        CloseSocket();
    }

    private void CloseSocket()
    {
        if (server != null)
        {
            server.Close();
            server = null;
        }
    }
}