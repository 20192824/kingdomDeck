using UnityEngine;

// 떨어지는 공에 붙입니다. 공이 떨어질 바닥 위치에 표시(마커)를 깔고,
// 공이 가까워질수록 마커가 커지고 밝아집니다. 공이 사라지면 마커도 사라집니다.
public class LandingMarker : MonoBehaviour
{
    [SerializeField, Tooltip("바닥에 깔 마커 프리팹 (납작한 원판)")]
    private GameObject markerPrefab;

    [SerializeField, Tooltip("바닥으로 인식할 태그")]
    private string groundTag = "Ground";

    // 위에서 내려다볼 때 공(지름 1m)에 가려지지 않도록 항상 공보다 크게 시작합니다.
    [SerializeField, Tooltip("공이 막 생겼을 때 마커 지름 (m). 공 지름(1m)보다 커야 위에서 보입니다.")]
    private float startSize = 1.3f;

    [SerializeField, Tooltip("공이 바닥에 닿기 직전 마커 지름 (m)")]
    private float endSize = 2.4f;

    [SerializeField, Tooltip("공이 멀 때 마커 색")]
    private Color farColor = new Color(0.45f, 0.1f, 0.1f);

    [SerializeField, Tooltip("공이 가까울 때 마커 색")]
    private Color nearColor = new Color(1f, 0.15f, 0.1f);

    [SerializeField, Tooltip("마커를 바닥에서 살짝 띄우는 높이 (바닥과 겹쳐 깜빡이지 않게)")]
    private float heightOffset = 0.02f;

    private Transform marker;
    private Renderer markerRenderer;
    private MaterialPropertyBlock block;
    private float groundY;
    private float startHeight;
    private float radius = 0.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        if (markerPrefab == null) return;

        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null) radius = sc.radius * transform.lossyScale.y;

        groundY = FindGroundY();
        startHeight = Mathf.Max(0.01f, transform.position.y - groundY - radius);

        GameObject go = Instantiate(markerPrefab);
        go.name = name + "_Marker";
        marker = go.transform;
        markerRenderer = go.GetComponentInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        UpdateMarker();
    }

    void LateUpdate()
    {
        if (marker != null) UpdateMarker();
    }

    void OnDestroy()
    {
        if (marker != null) Destroy(marker.gameObject);
    }

    void UpdateMarker()
    {
        // 0 = 막 생겼을 때, 1 = 바닥에 닿기 직전
        float height = transform.position.y - groundY - radius;
        float progress = 1f - Mathf.Clamp01(height / startHeight);

        float size = Mathf.Lerp(startSize, endSize, progress);
        Vector3 s = marker.localScale;
        marker.position = new Vector3(transform.position.x, groundY + heightOffset, transform.position.z);
        marker.localScale = new Vector3(size, s.y, size);

        if (markerRenderer != null)
        {
            markerRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(farColor, nearColor, progress));
            markerRenderer.SetPropertyBlock(block);
        }
    }

    // 공 바로 아래의 바닥 높이를 찾습니다. 못 찾으면 0.
    float FindGroundY()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (!string.IsNullOrEmpty(groundTag) && h.collider.CompareTag(groundTag) && h.point.y > best)
            {
                best = h.point.y;
                found = true;
            }
        }
        return found ? best : 0f;
    }
}
