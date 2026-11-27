using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 버블 게임 진행: 누가 물줄기에 맞으면 결과 화면을 띄우고, R 키(또는 버튼)로 다시 시작합니다.
public class BubbleGameManager : MonoBehaviour
{
    [SerializeField] private Bomber player;
    [SerializeField] private Bomber bot;

    [Header("결과 화면")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text hintText;
    [SerializeField] private Button restartButton;

    [SerializeField, Tooltip("첫 사망 후 결과를 보여주기까지 기다리는 시간 (둘 다 맞았는지 확인)")]
    private float resultDelay = 0.6f;

    private bool ended;
    private bool resultShown;

    public bool Ended => ended;

    void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (player != null) player.Died += OnSomeoneDied;
        if (bot != null) bot.Died += OnSomeoneDied;
    }

    void OnDestroy()
    {
        if (player != null) player.Died -= OnSomeoneDied;
        if (bot != null) bot.Died -= OnSomeoneDied;
    }

    void Update()
    {
        if (ended && RestartPressed()) Restart();
    }

    void OnSomeoneDied(Bomber who)
    {
        if (ended) return;
        ended = true;
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
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
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
