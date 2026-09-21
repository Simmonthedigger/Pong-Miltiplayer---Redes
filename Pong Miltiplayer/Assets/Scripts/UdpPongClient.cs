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
    private float remoteInitialX;

    [Header("Configurações do Jogador")]
    public GameObject localCube;
    public GameObject remoteCube;
    public float speed = 10f;
    public float minY = -3.8f;
    public float maxY = 3.8f;

    [Header("Configuração de Rede")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 5001;

    void Start()
    {
        if (remoteCube != null)
        {
            remotePos = remoteCube.transform.position;
            remoteInitialX = remoteCube.transform.position.x;
        }

        try
        {
            client = new UdpClient(0);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            // Log informando a tentativa de conexão
            Debug.Log($"<color=yellow>[Cliente] Tentando conectar ao servidor em {serverIP}:{serverPort}...</color>");

            receiveThread = new Thread(ReceiveData) { IsBackground = true };
            receiveThread.Start();

            // Envia mensagem HELLO
            SendNetworkMessage("HELLO");
            Debug.Log($"<color=cyan>[Cliente] Pacote HELLO enviado para {serverIP}:{serverPort}</color>");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Cliente Erro ao Iniciar]: " + ex.Message);
        }
    }

    void Update()
    {
        float v = Input.GetAxisRaw("Vertical");
        if (v != 0 && localCube != null)
        {
            Vector3 pos = localCube.transform.position;
            pos.y += v * speed * Time.deltaTime;
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            localCube.transform.position = pos;
        }

        if (localCube != null && myId != -1)
        {
            string msg = "POS:" +
                localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

            SendNetworkMessage(msg);
        }

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
                        // Mensagem clara informando que a conexão foi estabelecida e registrada com sucesso!
                        Debug.Log($"<color=green><b>[Cliente CONECTADO!]</b> Registrado no Servidor ({remoteEP.Address}). ID Atribuído = {myId}</color>");
                    }
                    else if (msg.StartsWith("POS:"))
                    {
                        string[] parts = msg.Substring(4).Split(';');
                        if (parts.Length == 3)
                        {
                            int id = int.Parse(parts[0]);
                            if (id != myId)
                            {
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