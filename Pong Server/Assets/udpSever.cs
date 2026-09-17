using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServerTwoClients : MonoBehaviour
{
    private UdpClient server;
    private IPEndPoint anyEP;
    private Thread receiveThread;
    private Dictionary<string, int> clientIds = new Dictionary<string, int>();
    private int nextId = 1;
    private readonly object lockObj = new object();

    void Start()
    {
        Application.targetFrameRate = 60;
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);
        receiveThread = new Thread(ReceiveData) { IsBackground = true };
        receiveThread.Start();
        Debug.Log("Servidor iniciado na porta 5001");
    }

    void ReceiveData()
    {
        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = anyEP.Address + ":" + anyEP.Port;

                lock (lockObj)
                {
                    if (!clientIds.ContainsKey(key) && clientIds.Count < 2)
                    {
                        clientIds[key] = nextId++;
                        string assignMsg = "ASSIGN:" + clientIds[key];
                        server.Send(Encoding.UTF8.GetBytes(assignMsg), assignMsg.Length, anyEP);
                    }

                    if (clientIds.TryGetValue(key, out int id))
                    {
                        // Retransmite mensagens de Posição (POS:) e Bola (BALL:) para todos os clientes
                        if (msg.StartsWith("POS:") || msg.StartsWith("BALL:"))
                        {
                            string prefix = msg.StartsWith("POS:") ? "POS:" : "BALL:";
                            string coords = msg.Substring(5);
                            string broadcast = $"{prefix}{id};{coords}";
                            byte[] bdata = Encoding.UTF8.GetBytes(broadcast);

                            foreach (var kvp in clientIds)
                            {
                                var parts = kvp.Key.Split(':');
                                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                                server.Send(bdata, bdata.Length, ep);
                            }
                        }
                    }
                }
            }
            catch (SocketException) { break; }
            catch (System.Exception) { }
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