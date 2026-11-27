using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>앞뒤로 흔들리는 애니메이션 (망치, 종, 깃발)</summary>
    public class Swinger : MonoBehaviour
    {
        public float speed = 9f;
        public float amplitude = 40f;
        Quaternion baseRot;

        void Awake() { baseRot = transform.localRotation; }

        void Update()
        {
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(Time.time * speed) * amplitude, 0, 0);
        }
    }
}
