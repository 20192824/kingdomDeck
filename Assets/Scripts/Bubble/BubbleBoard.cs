using System.Collections.Generic;
using UnityEngine;

// 버블 게임 보드: 바닥을 1칸짜리 격자로 나누고, 물풍선과 물줄기의 위치를 관리합니다.
public class BubbleBoard : MonoBehaviour
{
    public static BubbleBoard Instance { get; private set; }

    public static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    [Header("격자")]
    [SerializeField, Tooltip("(0,0) 칸의 왼쪽 아래 모서리 월드 좌표 (X, Z)")]
    private Vector2 origin = new Vector2(-10f, -10f);

    [SerializeField, Tooltip("가로 칸 수")]
    private int width = 20;

    [SerializeField, Tooltip("세로 칸 수")]
    private int height = 20;

    [SerializeField, Tooltip("한 칸 크기 (m)")]
    private float cellSize = 1f;

    [Header("물풍선")]
    [SerializeField] private WaterBalloon balloonPrefab;
    [SerializeField] private WaterStream streamPrefab;

    [SerializeField, Tooltip("물풍선이 터지기까지 걸리는 시간 (초)")]
    private float balloonFuse = 2.5f;

    [SerializeField, Tooltip("물줄기가 뻗어 나가는 칸 수 (십자 방향 각각)")]
    private int balloonRange = 2;

    [SerializeField, Tooltip("물줄기가 남아 있는 시간 (초). 이 동안 들어온 캐릭터도 맞습니다.")]
    private float streamDuration = 0.5f;

    private readonly Dictionary<Vector2Int, WaterBalloon> balloons = new Dictionary<Vector2Int, WaterBalloon>();
    private readonly Dictionary<Vector2Int, float> streamUntil = new Dictionary<Vector2Int, float>();

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public float BalloonFuse => balloonFuse;
    public int BalloonRange => balloonRange;
    public IEnumerable<WaterBalloon> Balloons => balloons.Values;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

    public Vector2Int WorldToCell(Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((p.z - origin.y) / cellSize);
        return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
    }

    public Vector3 CellToWorld(Vector2Int c, float y = 0f)
    {
        return new Vector3(origin.x + (c.x + 0.5f) * cellSize, y, origin.y + (c.y + 0.5f) * cellSize);
    }

    public bool HasBalloon(Vector2Int c) => balloons.ContainsKey(c);

    public bool IsStream(Vector2Int c) => streamUntil.TryGetValue(c, out float t) && t > Time.time;

    // 해당 칸에 물풍선을 놓습니다. 이미 있거나 보드 밖이면 null.
    public WaterBalloon PlaceBalloon(Bomber owner, Vector2Int cell)
    {
        if (balloonPrefab == null || !InBounds(cell) || HasBalloon(cell)) return null;

        WaterBalloon b = Instantiate(balloonPrefab, CellToWorld(cell, 0.4f), Quaternion.identity);
        b.Init(this, owner, cell, balloonRange, balloonFuse);
        balloons[cell] = b;
        return b;
    }

    internal void RemoveBalloon(Vector2Int cell, WaterBalloon b)
    {
        if (balloons.TryGetValue(cell, out WaterBalloon existing) && existing == b)
            balloons.Remove(cell);
    }

    // center에서 십자로 range칸까지 물줄기가 닿는 칸들. 보드 끝에서 멈추고,
    // 다른 물풍선이 있는 칸은 포함한 뒤 그 방향은 멈춥니다(그 풍선이 연쇄로 터짐).
    public List<Vector2Int> BlastCells(Vector2Int center, int range)
    {
        var cells = new List<Vector2Int> { center };
        foreach (Vector2Int dir in Directions)
        {
            for (int i = 1; i <= range; i++)
            {
                Vector2Int c = center + dir * i;
                if (!InBounds(c)) break;
                cells.Add(c);
                if (HasBalloon(c)) break;
            }
        }
        return cells;
    }

    internal void SpawnStreams(List<Vector2Int> cells)
    {
        float until = Time.time + streamDuration;
        foreach (Vector2Int c in cells)
        {
            streamUntil[c] = until;
            if (streamPrefab != null)
            {
                WaterStream s = Instantiate(streamPrefab, CellToWorld(c, 0.15f), Quaternion.identity);
                s.Init(this, c, streamDuration);
            }
        }
    }

    // 지금 놓인 물풍선들이 터지면 물이 닿을 칸 + 지금 물줄기가 있는 칸. (봇이 피할 때 사용)
    public HashSet<Vector2Int> GetDangerCells()
    {
        var danger = new HashSet<Vector2Int>();
        foreach (var kv in balloons)
            foreach (Vector2Int c in BlastCells(kv.Key, kv.Value.Range))
                danger.Add(c);
        foreach (var kv in streamUntil)
            if (kv.Value > Time.time) danger.Add(kv.Key);
        return danger;
    }

    // 씬 뷰에 격자 범위를 보여줍니다.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 c = new Vector3(origin.x + width * cellSize * 0.5f, 0.05f, origin.y + height * cellSize * 0.5f);
        Gizmos.DrawWireCube(c, new Vector3(width * cellSize, 0.1f, height * cellSize));
    }
}
