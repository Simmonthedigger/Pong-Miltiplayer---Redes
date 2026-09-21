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
    private Vector3 remotePos = Vector3.zero;

    [Header("Configurações do Jogador")]
    public GameObject localCube;
    public GameObject remoteCube;
    public float speed = 10f;
    public float minY = -3.8f;
    public float maxY = 3.8f;

    [Header("Configuração de Rede")]
    public string serverIP = "10.57.1.70";

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(serverIP), 5001);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData) { IsBackground = true };
        receiveThread.Start();

        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);
    }

    void Update()
    {
        // 1. Movimento Local Apenas Vertical
        float v = Input.GetAxisRaw("Vertical");
        if (v != 0 && localCube != null)
        {
            Vector3 pos = localCube.transform.position;
            pos.y += v * speed * Time.deltaTime;
            pos.y = Mathf.Clamp(pos.y, minY, maxY); // Limite de altura
            localCube.transform.position = pos;
        }

        // 2. Envia posição atualizada para o servidor
        if (localCube != null)
        {
            string msg = "POS:" +
                localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
            byte[] data = Encoding.UTF8.GetBytes(msg);
            client.Send(data, data.Length);
        }

        // 3. Atualiza posição do jogador remoto suavemente
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
                        Debug.Log("[Cliente] Meu ID = " + myId);
                    }
                    else if (msg.StartsWith("POS:"))
                    {
                        string[] parts = msg.Substring(4).Split(';');
                        if (parts.Length == 3)
                        {
                            int id = int.Parse(parts[0]);
                            if (id != myId)
                            {
                                float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                                float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                                remotePos = new Vector3(x, y, 0);
                            }
                        }
                    }

                    else if (msg.StartsWith("BALL:"))
                    {
                        string[] parts = msg.Substring(5).Split(';');
                        if (parts.Length == 3)
                        {
                            int id = int.Parse(parts[0]);
                            // Se for o Player 2 recebendo a posição enviada pelo Player 1
                            if (id != myId && pongBall != null)
                            {
                                float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                                float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                                pongBall.UpdateRemotePosition(x, y);
                            }
                        }
                    }
                }
            }
            catch (SocketException) { break; }
            catch (System.Exception) { }
        }
    }

    public void SendNetworkMessage(string msg)
    {
        if (client != null)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            client.Send(data, data.Length);
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