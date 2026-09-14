using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    public Transform player1Transform;
    public Transform player2Transform;
    public Transform ballTransform;
    public float moveSpeed = 10f;

    private float localY = 0f;

    void Update()
    {
        if (!UdpPongClient.Instance.isConnected) return;

        // Captura Input Local
        float v = Input.GetAxis("Vertical");
        localY = Mathf.Clamp(localY + v * moveSpeed * Time.deltaTime, -3.5f, 3.5f);

        // Envia Posição Local ao Servidor
        UdpPongClient.Instance.SendInput(localY);

        // Atualiza Posição dos Objetos com base nos dados sincronizados
        ballTransform.position = Vector3.Lerp(ballTransform.position, UdpPongClient.Instance.ballPos, Time.deltaTime * 25f);

        Vector3 targetP1 = new Vector3(player1Transform.position.x, UdpPongClient.Instance.p1Y, 0);
        Vector3 targetP2 = new Vector3(player2Transform.position.x, UdpPongClient.Instance.p2Y, 0);

        player1Transform.position = Vector3.Lerp(player1Transform.position, targetP1, Time.deltaTime * 20f);
        player2Transform.position = Vector3.Lerp(player2Transform.position, targetP2, Time.deltaTime * 20f);
    }
}