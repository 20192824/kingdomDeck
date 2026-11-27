using System.Collections.Generic;
using UnityEngine;

// 일정 시간마다 하늘의 랜덤 위치에 프리팹을 생성합니다.
// 시간이 지날수록 낙하 속도가 빨라지고, 한 번에 떨어지는 개수도 늘어납니다.
public class Spawner : MonoBehaviour
{
    [SerializeField, Tooltip("떨어뜨릴 프리팹 (Rigidbody가 붙어 있어야 떨어집니다)")]
    private GameObject prefab;

    [SerializeField, Tooltip("생성 간격 (초)")]
    private float interval = 3f;

    [SerializeField, Tooltip("생성 높이 (월드 Y)")]
    private float spawnHeight = 15f;

    [SerializeField, Tooltip("이 오브젝트 위치를 중심으로 한 생성 범위 (X, Z 크기). 공은 항상 이 범위 안에서만 생성됩니다.")]
    private Vector2 areaSize = new Vector2(18f, 18f);

    [Header("플레이어 주변에 생성")]
    [SerializeField, Tooltip("이 대상의 위치 주변에 생성합니다. 비워 두면 생성 범위 전체에서 랜덤으로 생성합니다.")]
    private Transform target;

    [SerializeField, Tooltip("대상 위치를 중심으로 이 반지름(m) 안의 랜덤 위치에 생성합니다")]
    private float spawnRadius = 4f;

    [SerializeField, Tooltip("생성된 오브젝트를 몇 초 뒤에 없앨지 (0이면 없애지 않음)")]
    private float lifetime = 0f;

    [Header("낙하 속도 (시간이 지날수록 빨라짐)")]
    [SerializeField, Tooltip("게임 시작 직후 생성될 때 아래로 주는 초기 속도 (m/s)")]
    private float startFallSpeed = 0f;

    [SerializeField, Tooltip("1초가 지날 때마다 초기 낙하 속도가 얼마나 늘어날지 (m/s)")]
    private float fallSpeedIncreasePerSecond = 0.5f;

    [SerializeField, Tooltip("이 시간(초)까지만 속도가 늘어나고 그 뒤로는 그때 속도가 최대 속도가 됩니다")]
    private float fallSpeedCapTime = 40f;

    [Header("동시에 떨어지는 개수 (시간이 지날수록 늘어남)")]
    [SerializeField, Tooltip("게임 시작 직후 한 번에 떨어지는 개수")]
    private int startSpawnCount = 1;

    [SerializeField, Tooltip("한 번에 떨어지는 최대 개수")]
    private int maxSpawnCount = 7;

    [SerializeField, Tooltip("몇 초마다 한 번에 떨어지는 개수가 1개씩 늘어날지")]
    private float secondsPerExtraSpawn = 10f;

    [SerializeField, Tooltip("같이 떨어지는 공끼리 최소 이 거리만큼 떨어뜨려 놓습니다")]
    private float minSpacing = 2f;

    private float timer;
    private float elapsed;

    // 지금 생성하면 공에 줄 초기 낙하 속도 (fallSpeedCapTime이 지나면 더 이상 늘지 않음)
    public float CurrentFallSpeed =>
        startFallSpeed + fallSpeedIncreasePerSecond * Mathf.Min(elapsed, fallSpeedCapTime);

    // 최대 낙하 속도 (= fallSpeedCapTime초 시점의 속도)
    public float MaxFallSpeed =>
        startFallSpeed + fallSpeedIncreasePerSecond * fallSpeedCapTime;

    // 지금 한 번에 떨어뜨릴 개수
    public int CurrentSpawnCount
    {
        get
        {
            int extra = secondsPerExtraSpawn > 0f ? Mathf.FloorToInt(elapsed / secondsPerExtraSpawn) : 0;
            return Mathf.Clamp(startSpawnCount + extra, 1, Mathf.Max(1, maxSpawnCount));
        }
    }

    void Update()
    {
        if (prefab == null) return;

        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            SpawnWave();
        }
    }

    // CurrentSpawnCount개를 서로 겹치지 않는 위치에 한꺼번에 떨어뜨립니다.
    public List<GameObject> SpawnWave()
    {
        int count = CurrentSpawnCount;
        var spawned = new List<GameObject>(count);
        var used = new List<Vector3>(count);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = RandomPosition();
            // 다른 공과 너무 가까우면 몇 번 다시 뽑습니다.
            for (int attempt = 0; attempt < 15 && TooClose(pos, used); attempt++)
                pos = RandomPosition();

            used.Add(pos);
            spawned.Add(SpawnAt(pos));
        }
        return spawned;
    }

    // 랜덤 위치에 1개만 떨어뜨립니다.
    public GameObject Spawn()
    {
        return SpawnAt(RandomPosition());
    }

    GameObject SpawnAt(Vector3 pos)
    {
        GameObject obj = Instantiate(prefab, pos, Random.rotation);

        // 시간이 지날수록 더 빠른 속도로 아래로 던집니다 (중력은 그대로 추가로 작용).
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.down * CurrentFallSpeed;

        if (lifetime > 0f) Destroy(obj, lifetime);
        return obj;
    }

    Vector3 RandomPosition()
    {
        Vector3 center = transform.position;
        float halfX = areaSize.x * 0.5f;
        float halfZ = areaSize.y * 0.5f;

        float x, z;
        if (target != null)
        {
            // 대상(플레이어) 위치를 중심으로 원 안의 랜덤 위치. 높이는 spawnHeight 그대로.
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            x = target.position.x + offset.x;
            z = target.position.z + offset.y;
        }
        else
        {
            x = center.x + Random.Range(-halfX, halfX);
            z = center.z + Random.Range(-halfZ, halfZ);
        }

        // 벽 밖으로 나가지 않도록 생성 범위 안으로 잘라 줍니다.
        x = Mathf.Clamp(x, center.x - halfX, center.x + halfX);
        z = Mathf.Clamp(z, center.z - halfZ, center.z + halfZ);
        return new Vector3(x, spawnHeight, z);
    }

    bool TooClose(Vector3 pos, List<Vector3> others)
    {
        float minSqr = minSpacing * minSpacing;
        foreach (Vector3 o in others)
            if ((o - pos).sqrMagnitude < minSqr) return true;
        return false;
    }

    // 씬 뷰에서 생성 범위를 노란 상자로, 플레이어 주변 생성 반경을 빨간 원으로 보여줍니다.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 c = new Vector3(transform.position.x, spawnHeight, transform.position.z);
        Gizmos.DrawWireCube(c, new Vector3(areaSize.x, 0.1f, areaSize.y));

        if (target != null)
        {
            Gizmos.color = Color.red;
            Vector3 tc = new Vector3(target.position.x, spawnHeight, target.position.z);
            const int segments = 32;
            Vector3 prev = tc + new Vector3(spawnRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = tc + new Vector3(Mathf.Cos(a) * spawnRadius, 0f, Mathf.Sin(a) * spawnRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
