using UnityEngine;

// 플레이어에게 닿으면 게임 오버를 일으키는 오브젝트에 붙입니다. (예: 떨어지는 공)
// 공이 가만히 있는 플레이어 위로 떨어지는 경우는 이쪽(Rigidbody 쪽)에서 감지합니다.
// 바닥(groundTag 태그)에 닿으면 바로 사라집니다.
public class Hazard : MonoBehaviour
{
    [SerializeField, Tooltip("닿으면 이 오브젝트가 사라지는 바닥의 태그")]
    private string groundTag = "Ground";

    void OnCollisionEnter(Collision collision)
    {
        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            player.GameOver();
            return;
        }

        if (!string.IsNullOrEmpty(groundTag) && collision.collider.CompareTag(groundTag))
            Destroy(gameObject);
    }
}
