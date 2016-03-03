using System;
using System.Data.HashFunction;
using System.Linq;
using System.Text;

namespace GesEventSpike.Matching
{
    public class MinHasher
    {
        private readonly xxHash _hasher = new xxHash();
        private readonly Tuple<int, int>[] _seeds;

        public MinHasher(Tuple<int, int>[] seeds)
        {
            _seeds = seeds;
        }

        public int[] GetMinHashSignature(string[] tokens)
        {
            var signature = Enumerable.Repeat(int.MaxValue, _seeds.Length).ToArray();

            foreach (var token in tokens.Distinct())
            {
                for (var i = 0; i < _seeds.Length; i++)
                {
                    var seeds = _seeds[i];
                    var currentHashValue = RandomHash(token, seeds.Item1, seeds.Item2);
                    if (currentHashValue < signature[i]) signature[i] = currentHashValue;
                }
            }

            return signature;
        }

        private int GetHash(int input)
        {
            return BitConverter.ToInt32(_hasher.ComputeHash(BitConverter.GetBytes(input)), 0);
        }

        private int GetHash(string input)
        {
            return BitConverter.ToInt32(_hasher.ComputeHash(Encoding.UTF8.GetBytes(input)), 0);
        }

        public int RandomHash(string inputData, int seedOne, int seedTwo)
        {
            // Faster, Does not throw exception for overflows during hashing.
            // Overflow is fine, just wrap
            unchecked
            {
                var hash = (int)2166136261;

                hash = hash * 16777619 ^ GetHash(seedOne);
                hash = hash * 16777619 ^ GetHash(seedTwo);
                hash = hash * 16777619 ^ GetHash(inputData);
                return hash;
            }
        }
    }
}