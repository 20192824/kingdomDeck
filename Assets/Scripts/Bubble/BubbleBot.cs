using System.Collections.Generic;
using UnityEngine;

// 테스트용 상대 봇: 격자를 한 칸씩 이동하며
// 1) 위험한 칸(곧 물줄기가 지나갈 칸)에 있으면 가장 가까운 안전한 칸으로 도망가고
// 2) 플레이어가 가까우면, 피할 곳이 있을 때만 물풍선을 놓고
// 3) 그 외에는 위험한 칸을 피하면서 플레이어 쪽으로 다가갑니다.
[RequireComponent(typeof(Bomber))]
public class BubbleBot : MonoBehaviour
{
    [SerializeField, Tooltip("노릴 대상 (플레이어)")]
    private Bomber target;

    [SerializeField, Tooltip("이동 속도 (초당 칸)")]
    private float moveSpeed = 3.5f;

    [SerializeField, Tooltip("물풍선을 놓은 뒤 다음 물풍선까지 최소 대기 시간 (초)")]
    private float placeCooldown = 1.5f;

    [SerializeField, Tooltip("플레이어와 이 칸 수 이내면 물풍선을 노립니다 (맨해튼 거리)")]
    private int attackDistance = 3;

    [SerializeField, Tooltip("다가갈 때 일부러 엉뚱한 칸으로 갈 확률 (0~1)")]
    [Range(0f, 1f)] private float wanderChance = 0.25f;

    [SerializeField, Tooltip("도망갈 때 남겨 둘 시간 여유 (초)")]
    private float escapeMargin = 0.4f;

    private Bomber bomber;
    private BubbleBoard board;
    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private readonly Queue<Vector2Int> path = new Queue<Vector2Int>();
    private float nextPlaceTime;
    private float idleUntil;
    private float height;

    void Awake()
    {
        bomber = GetComponent<Bomber>();
    }

    void Start()
    {
        board = BubbleBoard.Instance;
        height = transform.position.y;
        if (board == null) return;
        currentCell = board.WorldToCell(transform.position);
        targetCell = currentCell;
        transform.position = board.CellToWorld(currentCell, height);
        nextPlaceTime = Time.time + 1f;
    }

    void Update()
    {
        if (board == null || bomber.IsDead) return;

        Vector3 goal = board.CellToWorld(targetCell, height);
        transform.position = Vector3.MoveTowards(transform.position, goal, moveSpeed * board.CellSize * Time.deltaTime);

        Vector3 dir = goal - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 900f * Time.deltaTime);

        if ((transform.position - goal).sqrMagnitude < 0.0004f)
        {
            currentCell = targetCell;
            if (Time.time >= idleUntil) Decide();
        }
    }

    void Decide()
    {
        HashSet<Vector2Int> danger = board.GetDangerCells();

        // 1) 위험하면 도망
        if (danger.Contains(currentCell))
        {
            if (path.Count == 0 || danger.Contains(LastOf(path)))
                SetPath(FindPath(currentCell, c => !danger.Contains(c)));
            StepOrIdle();
            return;
        }

        // 지금 안전하면 도망 경로는 버립니다.
        path.Clear();

        // 2) 플레이어가 가까우면 공격 (피할 곳이 있을 때만)
        if (ShouldAttack() && TryAttack(danger)) { StepOrIdle(); return; }

        // 3) 이동: 위험한 칸은 피하고 플레이어 쪽으로
        Vector2Int? next = ChooseWanderStep(danger);
        if (next.HasValue) targetCell = next.Value;
        else idleUntil = Time.time + 0.2f;
    }

    bool ShouldAttack()
    {
        if (target == null || target.IsDead || !bomber.CanPlaceBalloon || Time.time < nextPlaceTime) return false;
        Vector2Int p = target.Cell;
        int dist = Manhattan(p, currentCell);
        bool inLine = (p.x == currentCell.x || p.y == currentCell.y) && dist <= board.BalloonRange;
        return inLine || dist <= attackDistance;
    }

    bool TryAttack(HashSet<Vector2Int> danger)
    {
        if (board.HasBalloon(currentCell)) return false;

        // 여기 풍선을 놓았다고 가정한 위험 지역
        var afterPlace = new HashSet<Vector2Int>(danger);
        foreach (Vector2Int c in board.BlastCells(currentCell, board.BalloonRange)) afterPlace.Add(c);

        List<Vector2Int> escape = FindPath(currentCell, c => !afterPlace.Contains(c), allowStartBalloon: true);
        if (escape == null) return false;

        float timeNeeded = escape.Count / Mathf.Max(0.1f, moveSpeed);
        if (timeNeeded > board.BalloonFuse - escapeMargin) return false;

        if (!bomber.TryPlaceBalloon()) return false;
        nextPlaceTime = Time.time + placeCooldown;
        SetPath(escape);
        return true;
    }

    Vector2Int? ChooseWanderStep(HashSet<Vector2Int> danger)
    {
        var options = new List<Vector2Int>();
        foreach (Vector2Int d in BubbleBoard.Directions)
        {
            Vector2Int c = currentCell + d;
            if (IsWalkable(c) && !danger.Contains(c)) options.Add(c);
        }
        if (options.Count == 0) return null;

        if (target != null && !target.IsDead && Random.value > wanderChance)
        {
            Vector2Int p = target.Cell;
            Vector2Int best = options[0];
            int bestDist = int.MaxValue;
            foreach (Vector2Int c in options)
            {
                int d = Manhattan(c, p);
                // 플레이어와 같은 칸으로는 굳이 들어가지 않습니다.
                if (d == 0) continue;
                if (d < bestDist || (d == bestDist && Random.value < 0.5f)) { best = c; bestDist = d; }
            }
            if (bestDist == int.MaxValue) return null;
            // 이미 바로 옆이면 가만히 있으면서 풍선 타이밍을 봅니다.
            if (Manhattan(currentCell, p) <= 1) return null;
            return best;
        }
        return options[Random.Range(0, options.Count)];
    }

    void StepOrIdle()
    {
        if (path.Count > 0) targetCell = path.Dequeue();
        else idleUntil = Time.time + 0.1f;
    }

    void SetPath(List<Vector2Int> cells)
    {
        path.Clear();
        if (cells == null) return;
        foreach (Vector2Int c in cells) path.Enqueue(c);
    }

    bool IsWalkable(Vector2Int c) => board.InBounds(c) && !board.HasBalloon(c) && !board.IsStream(c);

    // BFS로 goal 조건을 만족하는 가장 가까운 칸까지의 경로 (시작 칸 제외). 없으면 null.
    List<Vector2Int> FindPath(Vector2Int start, System.Func<Vector2Int, bool> isGoal, bool allowStartBalloon = true)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        prev[start] = start;

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (c != start && isGoal(c))
            {
                var result = new List<Vector2Int>();
                for (Vector2Int k = c; k != start; k = prev[k]) result.Add(k);
                result.Reverse();
                return result;
            }
            foreach (Vector2Int d in BubbleBoard.Directions)
            {
                Vector2Int n = c + d;
                if (prev.ContainsKey(n) || !IsWalkable(n)) continue;
                prev[n] = c;
                queue.Enqueue(n);
            }
        }
        return null;
    }

    static Vector2Int LastOf(Queue<Vector2Int> q)
    {
        Vector2Int last = default;
        foreach (Vector2Int c in q) last = c;
        return last;
    }

    static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
