using UnityEngine;

// 물풍선이 터진 칸에 잠깐 남는 물줄기. 남아 있는 동안 이 칸에 있는 캐릭터는 죽습니다.
public class WaterStream : MonoBehaviour
{
    private BubbleBoard board;
    private Vector2Int cell;
    private float until;
    private float duration;
    private Vector3 baseScale;

    public void Init(BubbleBoard board, Vector2Int cell, float duration)
    {
        this.board = board;
        this.cell = cell;
        this.duration = duration;
        until = Time.time + duration;
        baseScale = transform.localScale;
        HitBombers();
    }

    void Update()
    {
        if (board == null) { Destroy(gameObject); return; }

        HitBombers();

        // 사라지기 직전에 납작하게 줄어듭니다.
        float left = until - Time.time;
        float k = Mathf.Clamp01(left / Mathf.Max(0.01f, duration * 0.4f));
        transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k);

        if (left <= 0f) Destroy(gameObject);
    }

    void HitBombers()
    {
        for (int i = Bomber.All.Count - 1; i >= 0; i--)
        {
            Bomber b = Bomber.All[i];
            if (b != null && !b.IsDead && board.WorldToCell(b.transform.position) == cell)
                b.Die();
        }
    }
}
