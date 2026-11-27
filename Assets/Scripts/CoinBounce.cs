using UnityEngine;

// 동전을 통통 튀게 하면서 회전시킵니다.
public class CoinBounce : MonoBehaviour
{
    [Tooltip("튀어 오르는 최대 높이")]
    public float bounceHeight = 1.5f;

    [Tooltip("초당 튀는 횟수")]
    public float bouncesPerSecond = 1.2f;

    [Tooltip("초당 회전 각도")]
    public float spinSpeed = 180f;

    [Tooltip("바닥에 닿을 때 찌그러지는 정도 (0 = 없음)")]
    public float squashAmount = 0.25f;

    Vector3 startPos;
    Vector3 startScale;

    void Start()
    {
        startPos = transform.position;
        startScale = transform.localScale;
    }

    void Update()
    {
        // 0~1 사이 위상. |sin|을 쓰면 바닥에서 튕겨 오르는 포물선 느낌이 납니다.
        float phase = Time.time * bouncesPerSecond * Mathf.PI;
        float height = Mathf.Abs(Mathf.Sin(phase));
        transform.position = startPos + Vector3.up * (height * bounceHeight);

        // 바닥 근처에서 살짝 찌그러졌다가 펴지기 (스쿼시 & 스트레치)
        float squash = (1f - Mathf.Clamp01(height * 4f)) * squashAmount;
        transform.localScale = new Vector3(
            startScale.x * (1f + squash),
            startScale.y,
            startScale.z * (1f - squash));

        // 세로로 선 동전을 월드 Y축 기준으로 회전
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
}
