using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>날개를 펄럭이는 간단한 애니메이션 (드래곤)</summary>
    public class Flapper : MonoBehaviour
    {
        public float speed = 7f;
        public float amplitude = 35f;
        Transform l, r;

        void Awake()
        {
            l = transform.Find("WingL");
            r = transform.Find("WingR");
        }

        void Update()
        {
            float a = Mathf.Sin(Time.time * speed * Anim.Speed) * amplitude;
            if (l != null) l.localRotation = Quaternion.Euler(0, 0, a);
            if (r != null) r.localRotation = Quaternion.Euler(0, 0, -a);
        }
    }
}
