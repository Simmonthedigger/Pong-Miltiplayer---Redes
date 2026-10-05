using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServer4Players : MonoBehaviour
{
    private UdpClient server;
    private Thread receiveThread;
    private Dictionary<string, int> clientIds = new Dictionary<string, int>();
    private Dictionary<int, IPEndPoint> clientEndpoints = new Dictionary<int, IPEndPoint>();
    private int nextId = 1;
    private readonly object lockObj = new object();

    void Start()
    {
        Application.targetFrameRate = 60;
        try
        {
            server = new UdpClient(5001);
            receiveThread = new Thread(ReceiveData) { IsBackground = true };
            receiveThread.Start();
            Debug.Log("<color=green><b>[SERVIDOR INICIADO]</b> Rodando para 4 jogadores na porta 5001!</color>");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("<color=red>[SERVIDOR ERRO] Falha ao iniciar na porta 5001:</color> " + ex.Message);
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);
                string clientKey = remoteEP.Address.ToString() + ":" + remoteEP.Port;

                lock (lockObj)
                {
                    // 1. Limite alterado para até 4 jogadores
                    if (!clientIds.ContainsKey(clientKey) && clientIds.Count < 4)
                    {
                        int assignedId = nextId++;
                        clientIds[clientKey] = assignedId;
                        clientEndpoints[assignedId] = new IPEndPoint(remoteEP.Address, remoteEP.Port);

                        string assignMsg = "ASSIGN:" + assignedId;
                        byte[] assignBytes = Encoding.UTF8.GetBytes(assignMsg);
                        server.Send(assignBytes, assignBytes.Length, remoteEP);

                        Debug.Log($"<color=cyan><b>[SERVIDOR]</b> Novo Cliente Registrado: {clientKey} -> ID {assignedId}</color>");
                    }

                    if (clientIds.TryGetValue(clientKey, out int senderId))
                    {
                        if (msg == "HELLO")
                        {
                            string assignMsg = "ASSIGN:" + senderId;
                            byte[] assignBytes = Encoding.UTF8.GetBytes(assignMsg);
                            server.Send(assignBytes, assignBytes.Length, remoteEP);
                            continue;
                        }

                        // Retransmite mensagens (POS, BALL, SCORE, REQUEST_*)
                        if (msg.StartsWith("POS:") || msg.StartsWith("BALL:") || msg.StartsWith("SCORE:") || msg.StartsWith("REQUEST_LAUNCH") || msg.StartsWith("REQUEST_RESTART"))
                        {
                            byte[] bdata;

                            if (msg.StartsWith("POS:") || msg.StartsWith("BALL:"))
                            {
                                int colonIndex = msg.IndexOf(':');
                                string prefix = msg.Substring(0, colonIndex + 1);
                                string payload = msg.Substring(colonIndex + 1);
                                string broadcastMsg = $"{prefix}{senderId};{payload}";
                                bdata = Encoding.UTF8.GetBytes(broadcastMsg);
                            }
                            else
                            {
                                bdata = Encoding.UTF8.GetBytes(msg);
                            }

                            foreach (var ep in clientEndpoints.Values)
                            {
                                server.Send(bdata, bdata.Length, ep);
                            }
                        }
                    }
                }
            }
            catch (SocketException) { break; }
            catch (System.Exception ex)
            {
                Debug.LogError("[Servidor Erro]: " + ex.Message);
            }
        }
    }

    private void OnDestroy() => CloseSocket();
    private void OnApplicationQuit() => CloseSocket();

    private void CloseSocket()
    {
        if (server != null)
        {
            server.Close();
            server = null;
        }
    }
}