using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 생존 시간을 세어 화면에 표시하고, 게임 오버 화면과 다시 시작을 담당합니다.
public class GameManager : MonoBehaviour
{
    [Header("연결")]
    [SerializeField, Tooltip("게임 오버 여부를 확인할 플레이어")]
    private PlayerController player;

    [SerializeField, Tooltip("좌상단 생존 시간 텍스트")]
    private Text timeText;

    [SerializeField, Tooltip("게임 오버 시 보여줄 패널 (처음엔 꺼 둡니다)")]
    private GameObject gameOverPanel;

    [SerializeField, Tooltip("게임 오버 패널 안의 최종 기록 텍스트")]
    private Text resultText;

    [SerializeField, Tooltip("다시 시작 버튼")]
    private Button restartButton;

    private float survivalTime;
    private bool ended;

    public float SurvivalTime => survivalTime;

    void Start()
    {
        survivalTime = 0f;
        ended = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        UpdateTimeText();
    }

    void Update()
    {
        if (!ended)
        {
            if (player != null && player.IsGameOver)
            {
                EndGame();
            }
            else
            {
                // 게임 오버 전까지만 시간을 더합니다 (게임 오버 시 시간이 멈춤).
                survivalTime += Time.deltaTime;
                UpdateTimeText();
            }
        }
        else if (RestartPressed())
        {
            Restart();
        }
    }

    void EndGame()
    {
        ended = true;
        UpdateTimeText();
        if (resultText != null) resultText.text = $"Survived: {Mathf.FloorToInt(survivalTime)} s";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // 현재 씬을 다시 불러와 처음부터 시작합니다.
    public void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    void UpdateTimeText()
    {
        if (timeText != null) timeText.text = $"Time: {Mathf.FloorToInt(survivalTime)} s";
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
