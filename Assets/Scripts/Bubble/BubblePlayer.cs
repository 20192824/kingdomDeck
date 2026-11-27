using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 버블 게임 플레이어: WASD로 이동하고 스페이스바로 지금 서 있는 칸에 물풍선을 놓습니다.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Bomber))]
public class BubblePlayer : MonoBehaviour
{
    [SerializeField, Tooltip("이동 속도 (초당 유닛)")]
    private float moveSpeed = 4.5f;

    [SerializeField, Tooltip("이동 방향으로 몸을 돌리는 속도 (초당 각도)")]
    private float turnSpeed = 900f;

    [SerializeField, Tooltip("중력 가속도")]
    private float gravity = -20f;

    private CharacterController controller;
    private Bomber bomber;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        bomber = GetComponent<Bomber>();
    }

    void Update()
    {
        if (bomber.IsDead) return;

        if (PlacePressed()) bomber.TryPlaceBalloon();

        Vector2 input = ReadMoveInput();
        Vector3 move = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    static bool PlacePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
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
