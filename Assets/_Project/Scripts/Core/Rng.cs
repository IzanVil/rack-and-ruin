namespace ServerGame.Core
{
    public sealed class Rng
    {
        const uint Fallback = 0x9E3779B9u;

        uint _state;

        public Rng(int seed)
        {
            uint x = (uint)seed + Fallback;
            x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
            x = (x ^ (x >> 13)) * 0xC2B2AE35u;
            x ^= x >> 16;
            _state = x != 0u ? x : Fallback;
        }

        public uint State
        {
            get => _state;
            set => _state = value != 0u ? value : Fallback;
        }

        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }
}
