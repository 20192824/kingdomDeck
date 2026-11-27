using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 졸라맨이 오른팔(어깨)을 돌려 칼을 휘두릅니다.
// 스페이스바 또는 마우스 왼쪽 클릭으로 휘두르고, autoSwing을 켜면 자동으로 반복합니다.
public class SwordSwing : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("회전시킬 오른쪽 어깨")]
    public Transform shoulder;
    [Tooltip("칼끝 위치 (궤적 효과용, 비워도 됨)")]
    public Transform swordTip;

    [Header("각도 (Z축, 0 = 팔을 아래로 내림)")]
    public float readyAngle = 150f;   // 칼을 머리 위로 든 자세
    public float windupAngle = 175f;  // 휘두르기 전 살짝 뒤로 젖힘
    public float strikeAngle = 10f;   // 내려친 끝 자세

    [Header("시간 (초)")]
    public float windupTime = 0.12f;
    public float strikeTime = 0.12f;
    public float holdTime = 0.08f;
    public float recoverTime = 0.35f;

    [Header("자동 휘두르기")]
    public bool autoSwing = true;
    public float autoInterval = 1.2f;

    [Header("궤적 효과")]
    public bool showTrail = true;

    bool swinging;
    float nextAutoTime;
    TrailRenderer trail;

    void Start()
    {
        SetAngle(readyAngle);
        nextAutoTime = Time.time + 0.5f;

        if (showTrail && swordTip != null)
        {
            trail = swordTip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 1f, 1f, 0.8f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.emitting = false;
        }
    }

    void Update()
    {
        if (SwingPressed() || (autoSwing && Time.time >= nextAutoTime))
            Swing();
    }

    // 외부에서 호출해도 됩니다.
    public void Swing()
    {
        if (swinging || shoulder == null) return;
        StartCoroutine(SwingRoutine());
    }

    IEnumerator SwingRoutine()
    {
        swinging = true;

        // 1) 살짝 뒤로 젖히기
        yield return RotateTo(readyAngle, windupAngle, windupTime, EaseOut);

        // 2) 빠르게 내려치기
        if (trail != null) trail.emitting = true;
        yield return RotateTo(windupAngle, strikeAngle, strikeTime, EaseIn);
        if (trail != null) trail.emitting = false;

        // 3) 잠깐 멈췄다가
        yield return new WaitForSeconds(holdTime);

        // 4) 원래 자세로 돌아오기
        yield return RotateTo(strikeAngle, readyAngle, recoverTime, EaseInOut);

        swinging = false;
        nextAutoTime = Time.time + autoInterval;
    }

    IEnumerator RotateTo(float from, float to, float duration, System.Func<float, float> ease)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAngle(Mathf.LerpUnclamped(from, to, ease(t / duration)));
            yield return null;
        }
        SetAngle(to);
    }

    void SetAngle(float z)
    {
        if (shoulder != null)
            shoulder.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    static float EaseIn(float t) => t * t;
    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

    static bool SwingPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return key || mouse;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
    }
}
