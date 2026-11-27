using System.Collections.Generic;
using UnityEngine;

// 일정 시간 뒤 터지며 십자 방향으로 물줄기를 뿜는 물풍선.
public class WaterBalloon : MonoBehaviour
{
    [SerializeField, Tooltip("터지기 전 커졌다 작아졌다 하는 정도")]
    private float pulseAmount = 0.08f;

    private BubbleBoard board;
    private Bomber owner;
    private Vector2Int cell;
    private int range;
    private float explodeAt;
    private bool exploded;
    private Collider col;
    private Vector3 baseScale;
    private readonly List<Collider> ignoring = new List<Collider>();

    public Vector2Int Cell => cell;
    public int Range => range;
    public Bomber Owner => owner;
    public float TimeLeft => Mathf.Max(0f, explodeAt - Time.time);

    public void Init(BubbleBoard board, Bomber owner, Vector2Int cell, int range, float fuse)
    {
        this.board = board;
        this.owner = owner;
        this.cell = cell;
        this.range = range;
        explodeAt = Time.time + fuse;
        baseScale = transform.localScale;
        col = GetComponent<Collider>();

        // 풍선을 놓는 순간 그 칸에 서 있던 캐릭터는 빠져나갈 때까지 통과할 수 있게 합니다.
        if (col != null)
        {
            foreach (Bomber b in Bomber.All)
            {
                if (HorizontalDistance(b.transform.position) > ReleaseDistance()) continue;
                foreach (Collider c in b.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(col, c, true);
                    ignoring.Add(c);
                }
            }
        }
    }

    void Update()
    {
        if (exploded) return;

        // 통과를 허용했던 캐릭터가 칸을 벗어나면 다시 막습니다.
        for (int i = ignoring.Count - 1; i >= 0; i--)
        {
            Collider c = ignoring[i];
            if (c == null || HorizontalDistance(c.transform.position) > ReleaseDistance())
            {
                if (c != null && col != null) Physics.IgnoreCollision(col, c, false);
                ignoring.RemoveAt(i);
            }
        }

        // 터지기 직전일수록 빠르게 출렁입니다.
        float t = TimeLeft;
        float speed = Mathf.Lerp(18f, 6f, Mathf.Clamp01(t / 2.5f));
        transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * speed) * pulseAmount);

        if (Time.time >= explodeAt) Explode();
    }

    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        board.RemoveBalloon(cell, this);
        if (owner != null) owner.OnBalloonGone();

        List<Vector2Int> cells = board.BlastCells(cell, range);

        // 물줄기에 닿은 다른 물풍선은 연쇄로 터집니다.
        var chained = new List<WaterBalloon>();
        foreach (WaterBalloon other in new List<WaterBalloon>(board.Balloons))
            if (other != this && cells.Contains(other.Cell)) chained.Add(other);

        board.SpawnStreams(cells);
        foreach (WaterBalloon other in chained) other.Explode();

        Destroy(gameObject);
    }

    float HorizontalDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    float ReleaseDistance() => board != null ? board.CellSize * 0.9f : 0.9f;
}
