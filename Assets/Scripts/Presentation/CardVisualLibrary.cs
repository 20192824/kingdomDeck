using System.Collections.Generic;
using UnityEngine;

namespace KingdomDeck.Presentation
{
    [CreateAssetMenu(menuName = "왕국의 덱/카드 연출 목록", fileName = "CardVisualLibrary")]
    public class CardVisualLibrary : ScriptableObject
    {
        public List<CardVisual> visuals = new List<CardVisual>();

        readonly Dictionary<string, CardVisual> cache = new Dictionary<string, CardVisual>();

        public CardVisual Get(string id)
        {
            if (cache.TryGetValue(id, out var v) && v != null) return v;
            v = visuals.Find(x => x != null && x.cardId == id);
            if (v == null) v = CardVisualDefaults.Create(id);
            cache[id] = v;
            return v;
        }
    }
}
