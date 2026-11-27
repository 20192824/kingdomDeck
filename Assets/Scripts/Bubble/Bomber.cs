using System;
using System.Collections.Generic;
using UnityEngine;

// 물풍선을 놓을 수 있고, 물줄기에 맞으면 죽는 캐릭터 (플레이어와 봇 공용).
public class Bomber : MonoBehaviour
{
    public static readonly List<Bomber> All = new List<Bomber>();

    [SerializeField, Tooltip("콘솔/결과 화면에 표시할 이름")]
    private string displayName = "Player";

    [SerializeField, Tooltip("동시에 놓을 수 있는 물풍선 개수")]
    private int maxBalloons = 1;

    [SerializeField, Tooltip("죽었을 때 몸 색 (물방울에 갇힌 느낌)")]
    private Color trappedColor = new Color(0.55f, 0.85f, 1f);

    private int activeBalloons;
    private bool dead;
    private Renderer[] renderers;

    public event Action<Bomber> Died;

    public string DisplayName => displayName;
    public bool IsDead => dead;
    public int ActiveBalloons => activeBalloons;
    public bool CanPlaceBalloon => !dead && activeBalloons < maxBalloons;

    public Vector2Int Cell =>
        BubbleBoard.Instance != null ? BubbleBoard.Instance.WorldToCell(transform.position) : Vector2Int.zero;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // 지금 서 있는 칸에 물풍선을 놓습니다.
    public bool TryPlaceBalloon()
    {
        BubbleBoard board = BubbleBoard.Instance;
        if (!CanPlaceBalloon || board == null) return false;
        if (board.PlaceBalloon(this, Cell) == null) return false;
        activeBalloons++;
        return true;
    }

    internal void OnBalloonGone()
    {
        activeBalloons = Mathf.Max(0, activeBalloons - 1);
    }

    public void Die()
    {
        if (dead) return;
        dead = true;
        Debug.Log($"{displayName} was hit by a water stream!");

        // 물방울에 갇힌 것처럼 하늘색으로 바꾸고 살짝 부풀립니다.
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", trappedColor);
            r.SetPropertyBlock(block);
        }
        transform.localScale *= 1.15f;

        Died?.Invoke(this);
    }
}
