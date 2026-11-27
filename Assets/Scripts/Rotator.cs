using UnityEngine;

// 오브젝트를 Y축 기준으로 계속 회전시킵니다.
public class Rotator : MonoBehaviour
{
    [Tooltip("초당 회전 각도 (도). 음수면 반대 방향으로 회전합니다.")]
    public float degreesPerSecond = 90f;

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
