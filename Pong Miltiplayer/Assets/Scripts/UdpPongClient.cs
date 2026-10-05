using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;

public class UdpClient4Players : MonoBehaviour
{
    private UdpClient client;
    private Thread receiveThread;
    private IPEndPoint serverEP;
    private readonly object lockObj = new object();

    public PongBallPhysics pongBall;
    public int myId = -1;

    [Header("Configurações do Jogador Local")]
    public Transform localPaddle;
    public float speed = 10f;
    public float minY = -3.8f;
    public float maxY = 3.8f;

    [Header("Mapeamento de Raquetes (1 a 4)")]
    // Arraste no Inspector as 4 raquetes da cena na ordem do ID (0 = ID 1, 1 = ID 2, etc.)
    // IDs 1 e 2 = Lado Esquerdo (Superior e Inferior)
    // IDs 3 e 4 = Lado Direito (Superior e Inferior)
    public Transform[] allPaddles = new Transform[4];

    private Dictionary<int, Vector3> remoteTargets = new Dictionary<int, Vector3>();
    private Dictionary<int, float> initialXPositions = new Dictionary<int, float>();

    [Header("Configuração de Rede")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 5001;

    void Start()
    {
        // Salva as posições X iniciais de todas as raquetes
        for (int i = 0; i < allPaddles.Length; i++)
        {
            if (allPaddles[i] != null)
            {
                int playerNumber = i + 1;
                initialXPositions[playerNumber] = allPaddles[i].position.x;
                remoteTargets[playerNumber] = allPaddles[i].position;
            }
        }

        try
        {
            client = new UdpClient(0);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            Debug.Log($"<color=yellow>[Cliente] Conectando a {serverIP}:{serverPort}...</color>");

            receiveThread = new Thread(ReceiveData) { IsBackground = true };
            receiveThread.Start();

            SendNetworkMessage("HELLO");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Cliente Erro ao Iniciar]: " + ex.Message);
        }
    }

    void Update()
    {
        // 1. Movimentação do Jogador Local
        float v = Input.GetAxisRaw("Vertical");
        if (v != 0 && localPaddle != null)
        {
            Vector3 pos = localPaddle.position;
            pos.y += v * speed * Time.deltaTime;
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            localPaddle.position = pos;
        }

        // 2. Envio da posição local para a rede
        if (localPaddle != null && myId != -1)
        {
            string msg = "POS:" +
                localPaddle.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                localPaddle.position.y.ToString("F2", CultureInfo.InvariantCulture);

            SendNetworkMessage(msg);
        }

        // 3. Interpolação (Lerp) dos outros 3 jogadores remotos
        lock (lockObj)
        {
            for (int i = 1; i <= 4; i++)
            {
                if (i != myId && i <= allPaddles.Length && allPaddles[i - 1] != null)
                {
                    if (remoteTargets.ContainsKey(i))
                    {
                        allPaddles[i - 1].position = Vector3.Lerp(
                            allPaddles[i - 1].position,
                            remoteTargets[i],
                            Time.deltaTime * 15f
                        );
                    }
                }
            }
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);

                lock (lockObj)
                {
                    if (msg.StartsWith("ASSIGN:"))
                    {
                        myId = int.Parse(msg.Substring(7));

                        // Vincula o Transform local à raquete correspondente ao ID atribuído
                        if (myId >= 1 && myId <= allPaddles.Length)
                        {
                            localPaddle = allPaddles[myId - 1];
                        }

                        Debug.Log($"<color=green><b>[Cliente CONECTADO!]</b> Registrado no Servidor. Seu ID é {myId}</color>");
                    }
                    else if (msg.StartsWith("POS:"))
                    {
                        string[] parts = msg.Substring(4).Split(';');
                        if (parts.Length == 3)
                        {
                            int senderId = int.Parse(parts[0]);
                            if (senderId != myId)
                            {
                                float y = float.Parse(parts[2], CultureInfo.InvariantCulture);

                                if (initialXPositions.TryGetValue(senderId, out float initX))
                                {
                                    remoteTargets[senderId] = new Vector3(initX, y, 0);
                                }
                            }
                        }
                    }
                    else if (msg.StartsWith("BALL:"))
                    {
                        string[] parts = msg.Substring(5).Split(';');
                        if (parts.Length == 3)
                        {
                            int id = int.Parse(parts[0]);
                            if (id != myId && pongBall != null)
                            {
                                float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                                float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                                pongBall.UpdateRemotePosition(x, y);
                            }
                        }
                    }
                    else if (msg.StartsWith("REQUEST_LAUNCH"))
                    {
                        if (myId == 1 && pongBall != null)
                        {
                            pongBall.RemoteRequestLaunch();
                        }
                    }
                    else if (msg.StartsWith("SCORE:"))
                    {
                        string[] parts = msg.Substring(6).Split(';');
                        if (parts.Length == 3 && pongBall != null)
                        {
                            int p1 = int.Parse(parts[0]);
                            int p2 = int.Parse(parts[1]);
                            int nextServer = int.Parse(parts[2]);

                            pongBall.UpdateScore(p1, p2, nextServer);
                        }
                    }
                    else if (msg.StartsWith("REQUEST_RESTART"))
                    {
                        if (pongBall != null)
                        {
                            pongBall.RestartGame();
                        }
                    }
                }
            }
            catch (SocketException) { break; }
            catch (System.Exception ex)
            {
                Debug.LogError("[Cliente Recv Erro]: " + ex.Message);
            }
        }
    }

    public void SendNetworkMessage(string msg)
    {
        if (client != null && serverEP != null)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            client.Send(data, data.Length, serverEP);
        }
    }

    private void OnDestroy() => CloseSocket();
    private void OnApplicationQuit() => CloseSocket();

    private void CloseSocket()
    {
        if (client != null)
        {
            client.Close();
            client = null;
        }
    }
}