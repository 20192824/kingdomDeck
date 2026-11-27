using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// WASD로 플레이어를 이동시킵니다. 이동하는 방향을 바라보고, 중력도 받습니다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Tooltip("이동 속도 (초당 유닛)")]
    private float moveSpeed = 5f;

    [SerializeField, Tooltip("이동 방향으로 몸을 돌리는 속도 (초당 각도)")]
    private float turnSpeed = 720f;

    [SerializeField, Tooltip("중력 가속도")]
    private float gravity = -20f;

    private CharacterController controller;
    private float verticalVelocity;
    private bool isGameOver;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public bool IsGameOver => isGameOver;

    // 게임 오버 처리: 콘솔에 출력하고 더 이상 움직이지 않습니다. 여러 번 불려도 한 번만 처리합니다.
    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("Game Over");
    }

    // 플레이어가 움직이다가 Hazard(떨어지는 공)에 부딪힌 경우
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.GetComponentInParent<Hazard>() != null)
            GameOver();
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (isGameOver) return;

        Vector2 input = ReadMoveInput();

        // 월드 기준 XZ 평면 이동. 대각선이 더 빨라지지 않도록 길이를 1로 제한합니다.
        Vector3 move = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        // 이동 방향을 부드럽게 바라보기
        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        // 중력: 땅에 닿아 있으면 살짝 눌러 붙이고, 공중이면 가속
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    static Vector2 ReadMoveInput()
    {
        Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return v;
        if (kb.wKey.isPressed) v.y += 1f;
        if (kb.sKey.isPressed) v.y -= 1f;
        if (kb.dKey.isPressed) v.x += 1f;
        if (kb.aKey.isPressed) v.x -= 1f;
#else
        v.x = Input.GetAxisRaw("Horizontal");
        v.y = Input.GetAxisRaw("Vertical");
#endif
        return v;
    }
}
