using System.Collections.Generic;
using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>소환 연출 방식</summary>
    public enum SummonStyle
    {
        /// <summary>전장에 잠깐 나타났다 사라짐</summary>
        Appear,
        /// <summary>동전 튀어오름</summary>
        Coins,
        /// <summary>화살 일제 사격</summary>
        ArrowVolley,
        /// <summary>투석기가 바위 발사</summary>
        Catapult,
        /// <summary>드래곤이 날아와 화염</summary>
        Dragon,
        /// <summary>상대 성으로 날아가 공격 (용병, 마녀)</summary>
        FlyToEnemy,
        /// <summary>성 안에서 폭발</summary>
        Explosion,
    }

    /// <summary>
    /// 카드별 연출 데이터. 규칙 데이터(cards.json)와 카드 ID로 연결된다.
    /// 그림/프리팹/효과음이 비어 있으면 코드로 만든 임시 버전을 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "왕국의 덱/카드 연출", fileName = "CardVisual")]
    public class CardVisual : ScriptableObject
    {
        public string cardId;
        [Tooltip("카드 테두리/이름띠 색")] public Color frameColor = Color.gray;
        [Tooltip("카드 그림 배경색")] public Color artColor = Color.gray;
        [Tooltip("카드 그림 (비우면 소환물을 촬영해서 사용)")] public Texture2D art;
        [Tooltip("소환물 프리팹 (비우면 코드로 만든 임시 모델)")] public GameObject summonPrefab;
        [Tooltip("효과음 (비우면 기본 합성음)")] public AudioClip sfx;
        public SummonStyle style = SummonStyle.Appear;
        [Tooltip("소환물 크기")] public float summonScale = 1f;
        [Tooltip("소환 연출 길이 (1x 기준 초)")] public float duration = 0.9f;
        [Tooltip("소환 연출 설명 (기획 메모)")] public string note;
    }

    /// <summary>연출 에셋이 없을 때 쓰는 기본값 (에디터 메뉴로 에셋을 만들 때도 사용)</summary>
    public static class CardVisualDefaults
    {
        struct Def
        {
            public string Frame, Art, Note;
            public SummonStyle Style;
            public float Scale, Duration;
            public Def(string frame, string art, SummonStyle style, string note, float scale = 1f, float duration = 0.9f)
            { Frame = frame; Art = art; Style = style; Note = note; Scale = scale; Duration = duration; }
        }

        static readonly Dictionary<string, Def> Defs = new Dictionary<string, Def>
        {
            { "copper",    new Def("#b06a3b", "#5a3a22", SummonStyle.Coins, "동전 튀어오름", 1f, 0.5f) },
            { "silver",    new Def("#9aa6b2", "#3c4550", SummonStyle.Coins, "은화 더미", 1f, 0.5f) },
            { "gold",      new Def("#e0b43c", "#6a4f12", SummonStyle.Coins, "금화 폭포", 1f, 0.6f) },
            { "archers",   new Def("#7d3a2a", "#3d4a2a", SummonStyle.ArrowVolley, "화살 일제 사격", 1f, 1.4f) },
            { "catapult",  new Def("#8a3322", "#4a3a2a", SummonStyle.Catapult, "투석기가 바위 발사", 1f, 1.8f) },
            { "dragon",    new Def("#a01e1e", "#3a1a2a", SummonStyle.Dragon, "드래곤이 날아와 화염", 1f, 2.6f) },
            { "traitor",   new Def("#4a2a5a", "#1e1424", SummonStyle.Explosion, "성 안에서 폭발", 1f, 1.0f) },
            { "wall",      new Def("#6a6f78", "#3a3f48", SummonStyle.Appear, "성벽이 솟아 공격을 막음") },
            { "altar",     new Def("#c9b98a", "#4a4060", SummonStyle.Appear, "제단의 빛이 카드를 태움") },
            { "village",   new Def("#6f8f4a", "#3d5a32", SummonStyle.Appear, "작은 마을이 솟아오름") },
            { "workshop",  new Def("#8a6a42", "#4a3a26", SummonStyle.Appear, "망치질하는 장인") },
            { "smithy",    new Def("#5e5e66", "#3a2420", SummonStyle.Appear, "불꽃 튀는 모루") },
            { "guild",     new Def("#9a7a3a", "#4a4026", SummonStyle.Appear, "상인 마차") },
            { "mercenary", new Def("#8c2f2f", "#3a2a2a", SummonStyle.FlyToEnemy, "용병이 상대 손패를 쳐냄", 1f, 1.2f) },
            { "market",    new Def("#b5873a", "#5a3a2a", SummonStyle.Appear, "시장 천막") },
            { "monastery", new Def("#8a8fa8", "#3a3f5a", SummonStyle.Appear, "종소리 울리는 수도원") },
            { "mine",      new Def("#6a5a4a", "#2a2420", SummonStyle.Appear, "광부가 원석을 캐냄") },
            { "witch",     new Def("#5e3a7a", "#24183a", SummonStyle.FlyToEnemy, "마녀가 날아가 저주를 던짐", 1f, 1.4f) },
            { "banquet",   new Def("#a8324a", "#4a2a2a", SummonStyle.Appear, "연회 식탁") },
        };

        public static CardVisual Create(string id)
        {
            var v = ScriptableObject.CreateInstance<CardVisual>();
            v.name = "Visual_" + id;
            v.cardId = id;
            Apply(v);
            return v;
        }

        public static void Apply(CardVisual v)
        {
            if (!Defs.TryGetValue(v.cardId, out var d)) d = new Def("#777777", "#333333", SummonStyle.Appear, "");
            v.frameColor = Palette.Hex(d.Frame);
            v.artColor = Palette.Hex(d.Art);
            v.style = d.Style;
            v.summonScale = d.Scale;
            v.duration = d.Duration;
            v.note = d.Note;
        }

        public static IEnumerable<string> Ids => Defs.Keys;
    }
}
