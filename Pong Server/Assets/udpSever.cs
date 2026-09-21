using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServerTwoClients : MonoBehaviour
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

            // Log de servidor iniciado formatado em verde e negrito
            Debug.Log("<color=green><b>[SERVIDOR INICIADO]</b> Rodando com sucesso na porta UDP 5001!</color>");
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
                    // 1. Registra o cliente usando IP e PORTA (Qualquer mensagem inicial registra)
                    if (!clientIds.ContainsKey(clientKey) && clientIds.Count < 2)
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
                        // 2. Se o cliente enviar HELLO novamente, apenas re-confirma o ID
                        if (msg == "HELLO")
                        {
                            string assignMsg = "ASSIGN:" + senderId;
                            byte[] assignBytes = Encoding.UTF8.GetBytes(assignMsg);
                            server.Send(assignBytes, assignBytes.Length, remoteEP);
                            Debug.Log($"<color=yellow>[SERVIDOR] Reenviado ASSIGN:{senderId} para {clientKey}</color>");
                            continue;
                        }

                        // 3. Retransmite mensagens de Posição, Bola, Placar, Saque e Reinício
                        if (msg.StartsWith("POS:") || msg.StartsWith("BALL:") || msg.StartsWith("SCORE:") || msg.StartsWith("REQUEST_LAUNCH") || msg.StartsWith("REQUEST_RESTART"))
                        {
                            byte[] bdata;

                            // Se for posição ou bola, anexa o ID de quem enviou (ex: POS:1;X;Y)
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
                                // SCORE:, REQUEST_LAUNCH e REQUEST_RESTART são retransmitidas exatamente como recebidas
                                bdata = Encoding.UTF8.GetBytes(msg);
                            }

                            // Transmite para TODOS os clientes conectados
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