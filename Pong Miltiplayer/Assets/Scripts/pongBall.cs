using UnityEngine;
using System.Globalization;

public class PongBallPhysics : MonoBehaviour
{
    public UdpClientTwoClients networkClient;
    public float ballSpeed = 8f;

    private Rigidbody2D rb;
    private Vector3 remoteBallPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        ResetBall();
    }

    void Update()
    {
        if (networkClient == null || networkClient.myId == -1) return;

        // PLAYER 1: Roda a física real e manda a posição pela rede
        if (networkClient.myId == 1)
        {
            // Garante que a velocidade se mantenha constante
            rb.linearVelocity = rb.linearVelocity.normalized * ballSpeed;

            // Ponto se sair da tela (Limites X)
            if (transform.position.x > 9f || transform.position.x < -9f)
            {
                ResetBall();
            }

            SendBallPosition();
        }
        // PLAYER 2: Desativa a física e só segue a posição que vem do Player 1
        else
        {
            if (rb.simulated) rb.simulated = false; // Desliga a física para não ter conflito

            transform.position = Vector3.Lerp(transform.position, remoteBallPos, Time.deltaTime * 20f);
        }
    }

    // Chamado pelo UdpClientTwoClients quando receber o pacote BALL:
    public void UpdateRemotePosition(float x, float y)
    {
        remoteBallPos = new Vector3(x, y, 0);
    }

    private void ResetBall()
    {
        transform.position = Vector3.zero;

        if (networkClient != null && networkClient.myId == 1)
        {
            float dirX = Random.Range(0, 2) == 0 ? -1f : 1f;
            float dirY = Random.Range(-0.5f, 0.5f);
            Vector2 direction = new Vector2(dirX, dirY).normalized;

            rb.linearVelocity = direction * ballSpeed;
        }
    }

    private void SendBallPosition()
    {
        string msg = "BALL:" +
            transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
            transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

        networkClient.SendNetworkMessage(msg);
    }
}