using System.Collections.Generic;
using UnityEngine;

// 테스트용 상대 봇. 칸 단위로 이동하며 칸에 도착할 때마다 다음 행동을 다시 정합니다.
// 1) 피하기: 물이 닿을 칸에 있으면, 물이 도착하는 "시간"까지 계산해서
//    제때 빠져나갈 수 있는 경로로 가장 가까운 안전한 칸에 갑니다. (연쇄 폭발 포함)
// 2) 공격: 여기 풍선을 놓으면 플레이어가 물줄기 범위에 들어가고, 자기는 피할 수 있을 때만 놓습니다.
//    플레이어가 피할 곳이 하나도 없어지는 "가두기" 상황이면 망설이지 않습니다.
// 3) 자리 잡기: 플레이어와 같은 줄(가로/세로)이면서 사거리 안이고, 사이에 풍선이 없는 칸으로 이동합니다.
// 4) 그 외에는 안전한 칸으로 돌아다닙니다.
[RequireComponent(typeof(Bomber))]
public class BubbleBot : MonoBehaviour
{
    [SerializeField, Tooltip("노릴 대상 (플레이어)")]
    private Bomber target;

    [SerializeField, Tooltip("이동 속도 (초당 칸)")]
    private float moveSpeed = 4f;

    [SerializeField, Tooltip("물풍선을 놓은 뒤 다음 물풍선까지 최소 대기 시간 (초)")]
    private float placeCooldown = 1.2f;

    [SerializeField, Tooltip("물줄기 시간 계산에 두는 여유 (초). 클수록 조심스럽게 피합니다.")]
    private float safetyMargin = 0.15f;

    [SerializeField, Tooltip("플레이어가 1초에 움직일 수 있다고 볼 칸 수 (가두기 판단용)")]
    private float playerCellsPerSecond = 4.5f;

    [SerializeField, Tooltip("가끔 일부러 실수할 확률 (0 = 항상 최선, 1 = 항상 엉뚱하게)")]
    [Range(0f, 1f)] private float randomness = 0.1f;

    private Bomber bomber;
    private BubbleBoard board;
    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private float nextPlaceTime;
    private float idleUntil;
    private float height;

    public string LastAction { get; private set; } = "";

    float StepTime => 1f / Mathf.Max(0.1f, moveSpeed);

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
        BubbleBoard.DangerMap danger = board.GetDangerMap();

        // 1) 피하기
        if (danger.IsDangerous(currentCell))
        {
            List<Vector2Int> escape = FindTimedPath(currentCell, c => !danger.IsDangerous(c), danger, null)
                                      ?? FindPath(currentCell, c => !danger.IsDangerous(c));
            if (escape != null && escape.Count > 0) { targetCell = escape[0]; LastAction = "escape"; }
            else { idleUntil = Time.time + 0.05f; LastAction = "stuck"; }
            return;
        }

        // 2) 공격
        if (TryAttack()) return;

        bool playerAlive = target != null && !target.IsDead;

        // 3) 자리 잡기 (가끔은 일부러 돌아다님)
        if (playerAlive && Random.value >= randomness)
        {
            Vector2Int p = target.Cell;
            System.Func<Vector2Int, bool> isAttackSpot = c => IsAttackSpot(c, p) && !danger.IsDangerous(c);

            if (isAttackSpot(currentCell))
            {
                // 이미 좋은 자리: 풍선 대기 시간이 끝날 때까지 잠깐 기다립니다.
                idleUntil = Time.time + 0.1f;
                LastAction = "hold";
                return;
            }

            List<Vector2Int> path = FindTimedPath(currentCell, isAttackSpot, danger, null);
            if (path != null && path.Count > 0) { targetCell = path[0]; LastAction = "approach"; return; }
        }

        // 4) 안전하게 돌아다니기
        Vector2Int? step = SafeRandomStep(danger);
        if (step.HasValue) { targetCell = step.Value; LastAction = "wander"; }
        else { idleUntil = Time.time + 0.15f; LastAction = "wait"; }
    }

    bool TryAttack()
    {
        if (target == null || target.IsDead || !bomber.CanPlaceBalloon) return false;
        if (Time.time < nextPlaceTime || board.HasBalloon(currentCell)) return false;

        // 여기 풍선을 놓았다고 가정한 위험 지도 (연쇄 포함)
        BubbleBoard.DangerMap hypo = board.GetDangerMap(currentCell);
        Vector2Int p = target.Cell;
        if (!hypo.IsDangerous(p)) return false;   // 플레이어에게 닿지 않으면 놓지 않음

        // 자기가 제때 빠져나갈 수 있어야 함 (놓은 풍선 칸은 이미 서 있으니 출발은 가능)
        List<Vector2Int> escape = FindTimedPath(currentCell, c => !hypo.IsDangerous(c), hypo, null);
        if (escape == null || escape.Count == 0) return false;

        bool trap = CountPlayerSafeCells(p, hypo, currentCell, 12) == 0;
        if (!trap && Random.value < randomness) return false;   // 가끔 망설임

        if (!bomber.TryPlaceBalloon()) return false;
        nextPlaceTime = Time.time + placeCooldown;
        targetCell = escape[0];
        LastAction = trap ? "trap" : "attack";
        return true;
    }

    // 플레이어와 같은 줄이고, 사거리 안이고, 사이에 물줄기를 막는 풍선이 없는 칸
    bool IsAttackSpot(Vector2Int c, Vector2Int p)
    {
        if (c == p || !board.InBounds(c)) return false;
        int range = board.BalloonRange;
        if (c.x == p.x && Mathf.Abs(c.y - p.y) <= range) return LineClear(c, p);
        if (c.y == p.y && Mathf.Abs(c.x - p.x) <= range) return LineClear(c, p);
        return false;
    }

    bool LineClear(Vector2Int a, Vector2Int b)
    {
        Vector2Int step = new Vector2Int(System.Math.Sign(b.x - a.x), System.Math.Sign(b.y - a.y));
        for (Vector2Int c = a + step; c != b; c += step)
            if (board.HasBalloon(c)) return false;
        return true;
    }

    // 플레이어가 물이 닿기 전에 도착할 수 있는 안전한 칸 수 (0이면 갇힘)
    int CountPlayerSafeCells(Vector2Int start, BubbleBoard.DangerMap map, Vector2Int extraBlocked, int maxSteps)
    {
        float step = 1f / Mathf.Max(0.1f, playerCellsPerSecond);
        var dist = new Dictionary<Vector2Int, int> { { start, 0 } };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        int safe = map.IsDangerous(start) ? 0 : 1;

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            int k = dist[c];
            if (k >= maxSteps) continue;
            foreach (Vector2Int d in BubbleBoard.Directions)
            {
                Vector2Int n = c + d;
                if (dist.ContainsKey(n) || n == extraBlocked || !IsWalkable(n)) continue;
                float arrive = (k + 1) * step;
                if (map.Overlaps(n, arrive - 0.5f * step, arrive + 0.5f * step, 0f)) continue;
                dist[n] = k + 1;
                queue.Enqueue(n);
                if (!map.IsDangerous(n)) safe++;
            }
        }
        return safe;
    }

    // 시간까지 고려한 BFS: 각 칸을 지나는 동안 물이 오지 않는 경로만 고릅니다.
    List<Vector2Int> FindTimedPath(Vector2Int start, System.Func<Vector2Int, bool> isGoal,
                                   BubbleBoard.DangerMap map, Vector2Int? extraBlocked)
    {
        float step = StepTime;
        var prev = new Dictionary<Vector2Int, Vector2Int> { { start, start } };
        var dist = new Dictionary<Vector2Int, int> { { start, 0 } };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (c != start && isGoal(c)) return Reconstruct(prev, start, c);

            int k = dist[c];
            foreach (Vector2Int d in BubbleBoard.Directions)
            {
                Vector2Int n = c + d;
                if (dist.ContainsKey(n) || !IsWalkable(n)) continue;
                if (extraBlocked.HasValue && n == extraBlocked.Value) continue;
                float arrive = (k + 1) * step;
                if (map.Overlaps(n, arrive - 0.5f * step, arrive + 0.5f * step, safetyMargin)) continue;
                dist[n] = k + 1;
                prev[n] = c;
                queue.Enqueue(n);
            }
        }
        return null;
    }

    // 시간을 고려하지 않는 BFS (시간 계산으로 길이 없을 때의 마지막 수단)
    List<Vector2Int> FindPath(Vector2Int start, System.Func<Vector2Int, bool> isGoal)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int> { { start, start } };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (c != start && isGoal(c)) return Reconstruct(prev, start, c);
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

    static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> prev, Vector2Int start, Vector2Int end)
    {
        var result = new List<Vector2Int>();
        for (Vector2Int k = end; k != start; k = prev[k]) result.Add(k);
        result.Reverse();
        return result;
    }

    Vector2Int? SafeRandomStep(BubbleBoard.DangerMap danger)
    {
        var options = new List<Vector2Int>();
        foreach (Vector2Int d in BubbleBoard.Directions)
        {
            Vector2Int c = currentCell + d;
            if (IsWalkable(c) && !danger.IsDangerous(c)) options.Add(c);
        }
        if (options.Count == 0) return null;
        return options[Random.Range(0, options.Count)];
    }

    bool IsWalkable(Vector2Int c) => board.InBounds(c) && !board.HasBalloon(c) && !board.IsStream(c);
}
