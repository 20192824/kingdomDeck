using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 버블 게임 진행:
// 타이틀 화면(엔터로 시작) -> 플레이 -> 누가 물줄기에 맞으면 결과 화면 -> R 키(또는 버튼)로 바로 다시 시작.
public class BubbleGameManager : MonoBehaviour
{
    public enum State { Title, Playing, Ended }

    [SerializeField] private Bomber player;
    [SerializeField] private Bomber bot;

    [Header("타이틀 화면")]
    [SerializeField, Tooltip("처음에 보여줄 타이틀 패널")]
    private GameObject titlePanel;

    [SerializeField, Tooltip("깜빡이는 '엔터를 눌러 시작' 문구")]
    private Text pressStartText;

    [SerializeField, Tooltip("타이틀 화면 동안 꺼 둘 조작/AI 스크립트 (플레이어 조작, 봇 AI)")]
    private Behaviour[] disableUntilStart;

    [Header("결과 화면")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text hintText;
    [SerializeField] private Button restartButton;

    [SerializeField, Tooltip("첫 사망 후 결과를 보여주기까지 기다리는 시간 (둘 다 맞았는지 확인)")]
    private float resultDelay = 0.6f;

    // R로 다시 시작할 때는 타이틀을 건너뛰고 바로 게임을 시작합니다.
    private static bool skipTitleOnce;

    // 에디터에서 Play를 새로 누를 때는 항상 타이틀부터 (도메인 리로드를 꺼 둔 경우 대비)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        skipTitleOnce = false;
    }

    private State state;
    private bool resultShown;

    public State CurrentState => state;
    public bool Ended => state == State.Ended;

    void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (player != null) player.Died += OnSomeoneDied;
        if (bot != null) bot.Died += OnSomeoneDied;

        if (skipTitleOnce)
        {
            skipTitleOnce = false;
            BeginGame();
        }
        else
        {
            ShowTitle();
        }
    }

    void OnDestroy()
    {
        if (player != null) player.Died -= OnSomeoneDied;
        if (bot != null) bot.Died -= OnSomeoneDied;
    }

    void Update()
    {
        switch (state)
        {
            case State.Title:
                if (pressStartText != null)
                {
                    // 문구를 천천히 깜빡이게 합니다.
                    Color c = pressStartText.color;
                    c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                    pressStartText.color = c;
                }
                if (StartPressed()) BeginGame();
                break;

            case State.Ended:
                if (RestartPressed()) Restart();
                break;
        }
    }

    void ShowTitle()
    {
        state = State.Title;
        SetControlsEnabled(false);
        if (titlePanel != null) titlePanel.SetActive(true);
    }

    public void BeginGame()
    {
        state = State.Playing;
        if (titlePanel != null) titlePanel.SetActive(false);
        SetControlsEnabled(true);
    }

    void SetControlsEnabled(bool enabled)
    {
        if (disableUntilStart == null) return;
        foreach (Behaviour b in disableUntilStart)
            if (b != null) b.enabled = enabled;
    }

    void OnSomeoneDied(Bomber who)
    {
        if (state == State.Ended) return;
        state = State.Ended;
        StartCoroutine(ShowResultAfterDelay());
    }

    IEnumerator ShowResultAfterDelay()
    {
        yield return new WaitForSeconds(resultDelay);
        ShowResult();
    }

    public void ShowResult()
    {
        if (resultShown) return;
        resultShown = true;

        bool playerDead = player != null && player.IsDead;
        bool botDead = bot != null && bot.IsDead;

        string title;
        Color color;
        if (playerDead && botDead) { title = "DRAW"; color = new Color(1f, 0.85f, 0.3f); }
        else if (playerDead) { title = "YOU LOSE"; color = new Color(1f, 0.35f, 0.3f); }
        else { title = "YOU WIN"; color = new Color(0.4f, 0.9f, 1f); }

        Debug.Log($"Bubble result: {title}");
        if (titleText != null) { titleText.text = title; titleText.color = color; }
        if (hintText != null) hintText.text = "Press R to restart";
        if (resultPanel != null) resultPanel.SetActive(true);
    }

    public void Restart()
    {
        skipTitleOnce = true;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    static bool StartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
