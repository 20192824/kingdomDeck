using System;
using System.Collections.Generic;

namespace KingdomDeck.Engine
{
    /// <summary>
    /// 저장/불러오기. 엔진은 시드로 완전히 재현되므로 "시드 + 설정 + 지금까지의 명령 목록"만 저장하고,
    /// 불러올 때는 같은 게임을 만들어 명령을 다시 적용한다.
    /// </summary>
    public sealed class SaveGame
    {
        public long Seed;
        public bool InstantCastleEnd = true;
        public int FirstPlayer;
        public List<string> Kingdom = new List<string>();
        public string[] PlayerNames;
        /// <summary>연출 쪽이 쓰는 추가 정보 (AI 난이도 등)</summary>
        public Dictionary<string, object> Extra = new Dictionary<string, object>();
        public List<Command> Commands = new List<Command>();

        public static SaveGame From(Game g)
        {
            return new SaveGame
            {
                Seed = g.Options.Seed,
                InstantCastleEnd = g.Options.InstantCastleEnd,
                FirstPlayer = g.FirstPlayer,
                Kingdom = new List<string>(g.Kingdom),
                PlayerNames = g.Options.PlayerNames,
                Commands = new List<Command>(g.CommandLog),
            };
        }

        public string ToJson()
        {
            var cmds = new List<object>();
            foreach (var c in Commands) cmds.Add(c.ToJson());
            var kingdom = new List<object>(Kingdom);
            var names = new List<object>();
            if (PlayerNames != null) foreach (var n in PlayerNames) names.Add(n);
            var d = new Dictionary<string, object>
            {
                { "version", 1 },
                { "seed", Seed },
                { "instantCastleEnd", InstantCastleEnd },
                { "firstPlayer", FirstPlayer },
                { "kingdom", kingdom },
                { "names", names },
                { "extra", Extra },
                { "commands", cmds },
            };
            return MiniJson.Serialize(d, true);
        }

        public static SaveGame FromJson(string json)
        {
            var d = (Dictionary<string, object>)MiniJson.Parse(json);
            var s = new SaveGame
            {
                Seed = MiniJson.Long(d, "seed"),
                InstantCastleEnd = MiniJson.Bool(d, "instantCastleEnd", true),
                FirstPlayer = MiniJson.Int(d, "firstPlayer"),
            };
            foreach (var k in MiniJson.List(d, "kingdom") ?? new List<object>()) s.Kingdom.Add(k.ToString());
            var names = MiniJson.List(d, "names");
            if (names != null && names.Count == 2) s.PlayerNames = new[] { names[0].ToString(), names[1].ToString() };
            s.Extra = MiniJson.Obj(d, "extra") ?? new Dictionary<string, object>();
            foreach (var c in MiniJson.List(d, "commands") ?? new List<object>())
                s.Commands.Add(Command.FromJson((Dictionary<string, object>)c));
            return s;
        }

        /// <summary>저장된 게임을 다시 만든다. 스냅샷 기록은 끈 상태로 재생하고 마지막에 켠다.</summary>
        public Game Restore(CardDatabase db, bool captureSnapshots = true)
        {
            var opt = new GameOptions
            {
                Seed = Seed,
                InstantCastleEnd = InstantCastleEnd,
                FirstPlayer = FirstPlayer,
                Kingdom = new List<string>(Kingdom),
                CaptureSnapshots = false,
            };
            if (PlayerNames != null) opt.PlayerNames = PlayerNames;
            var g = new Game(db, opt);
            g.Start();
            foreach (var c in Commands)
                if (!g.Submit(c)) throw new InvalidOperationException("저장 파일 재생 실패: " + c + " — " + g.LastError);
            opt.CaptureSnapshots = captureSnapshots;
            return g;
        }
    }
}
