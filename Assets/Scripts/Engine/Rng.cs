namespace KingdomDeck.Engine
{
    /// <summary>
    /// 시드 기반 난수 (xorshift64*). 같은 시드 → 같은 게임.
    /// </summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(long seed)
        {
            // splitmix64로 시드를 섞어 0 상태를 피한다
            ulong z = (ulong)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            state = z == 0 ? 0x2545F4914F6CDD1DUL : z;
        }

        public ulong NextULong()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>0 이상 max 미만의 정수</summary>
        public int Next(int max)
        {
            if (max <= 1) return 0;
            return (int)(NextULong() % (ulong)max);
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
