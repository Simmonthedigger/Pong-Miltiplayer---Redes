using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpPongClient : MonoBehaviour
{
    public static UdpPongClient Instance;

    private UdpClient client;
    private Thread receiveThread;
    private IPEndPoint serverEP;

    public int myId = -1;
    public bool isConnected = false;

    // Dados Sincronizados
    public Vector3 ballPos;
    public float p1Y, p2Y;
    public int scoreP1, scoreP2;
    public int winnerId = -1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ConnectToServer(string ip)
    {
        if (isConnected) return;

        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(ip), 5001);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);
    }

    public void SendInput(float yPosition)
    {
        if (!isConnected) return;
        string msg = "POS:" + yPosition.ToString("F2", CultureInfo.InvariantCulture);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);
    }

    public void SendRestart()
    {
        if (!isConnected) return;
        byte[] data = Encoding.UTF8.GetBytes("RESTART");
        client.Send(data, data.Length);
    }

    private void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);

                if (msg.StartsWith("ASSIGN:"))
                {
                    myId = int.Parse(msg.Substring(7));
                    isConnected = true;
                }
                else if (msg.StartsWith("STATE:"))
                {
                    string[] p = msg.Substring(6).Split(';');
                    if (p.Length == 6)
                    {
                        ballPos = new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), 0);
                        p1Y = float.Parse(p[2], CultureInfo.InvariantCulture);
                        p2Y = float.Parse(p[3], CultureInfo.InvariantCulture);
                        scoreP1 = int.Parse(p[4]);
                        scoreP2 = int.Parse(p[5]);
                    }
                }
                else if (msg.StartsWith("WIN:"))
                {
                    winnerId = int.Parse(msg.Substring(4));
                }
            }
            catch (System.Exception) { break; }
        }
    }

    private void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}