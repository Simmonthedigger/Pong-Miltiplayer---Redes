using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpClientTwoClients : MonoBehaviour
{
    private UdpClient client;
    private Thread receiveThread;
    private IPEndPoint serverEP;
    private readonly object lockObj = new object();

    public PongBallPhysics pongBall;
    public int myId = -1;

    private Vector3 remotePos;
    private float remoteInitialX; // Mantém a coluna X fixa do cliente local

    [Header("Configurações do Jogador")]
    public GameObject localCube;
    public GameObject remoteCube;
    public float speed = 10f;
    public float minY = -3.8f;
    public float maxY = 3.8f;

    [Header("Configuração de Rede")]
    public string serverIP = "10.57.1.70";
    public int serverPort = 5001;

    void Start()
    {
        // 1. Grava a posição X inicial para o cubo remoto não ir para o centro (0,0,0)
        if (remoteCube != null)
        {
            remotePos = remoteCube.transform.position;
            remoteInitialX = remoteCube.transform.position.x;
        }

        try
        {
            // Cria o socket em uma porta dinamicamente atribuída pelo sistema (porta 0)
            client = new UdpClient(0);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            // Inicia thread de recepção de dados
            receiveThread = new Thread(ReceiveData) { IsBackground = true };
            receiveThread.Start();

            // Envia mensagem HELLO para registrar no servidor
            byte[] hello = Encoding.UTF8.GetBytes("HELLO");
            client.Send(hello, hello.Length, serverEP);
            Debug.Log("[Cliente] Enviou HELLO para o servidor.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Cliente Error]: " + ex.Message);
        }
    }

    void Update()
    {
        // 1. Movimento do Cubo Local
        float v = Input.GetAxisRaw("Vertical");
        if (v != 0 && localCube != null)
        {
            Vector3 pos = localCube.transform.position;
            pos.y += v * speed * Time.deltaTime;
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            localCube.transform.position = pos;
        }

        // 2. Envia posição atualizada para o servidor
        if (localCube != null && myId != -1)
        {
            string msg = "POS:" +
                localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

            SendNetworkMessage(msg);
        }

        // 3. Atualiza a posição do cubo remoto
        lock (lockObj)
        {
            if (remoteCube != null)
            {
                remoteCube.transform.position = Vector3.Lerp(
                    remoteCube.transform.position,
                    remotePos,
                    Time.deltaTime * 15f
                );
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
                        Debug.Log("[Cliente] Atribuído ID = " + myId);
                    }
                    else if (msg.StartsWith("POS:"))
                    {
                        string[] parts = msg.Substring(4).Split(';');
                        if (parts.Length == 3)
                        {
                            int id = int.Parse(parts[0]);
                            if (id != myId)
                            {
                                // Se recebemos a posição de outro jogador, confirma que o Player 2 está no jogo!
                                if (pongBall != null)
                                {
                                    pongBall.SetPlayer2Connected();
                                }

                                float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                                remotePos = new Vector3(remoteInitialX, y, 0);
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

                    // Adicione dentro de ReceiveData() no lock(lockObj):

                    else if (msg.StartsWith("REQUEST_LAUNCH"))
                    {
                        if (myId == 1 && pongBall != null)
                        {
                            pongBall.RemoteRequestLaunch();
                        }
                    }
                    else if (msg.StartsWith("SCORE:"))
                    {
                        // Formato SCORE:p1;p2;servingPlayerId
                        string[] parts = msg.Substring(6).Split(';');
                        if (parts.Length == 3 && pongBall != null)
                        {
                            int p1 = int.Parse(parts[0]);
                            int p2 = int.Parse(parts[1]);
                            int nextServer = int.Parse(parts[2]);

                            pongBall.UpdateScore(p1, p2, nextServer);
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
        if (client != null)
        {
            client.Close();
            client = null;
        }
    }
}