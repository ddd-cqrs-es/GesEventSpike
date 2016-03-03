using System;
using System.Collections.Generic;

namespace GesEventSpike.Matching
{
    public class MinHashSeedFactory
    {
        public static Tuple<int, int>[] Create(int length)
        {
            var seeds = new Tuple<int, int>[length];
            var hashSet = new HashSet<int>();
            var random = new Random();

            for (var index = 0; index < length; index++)
            {
                var seed = new Tuple<int, int>(random.Next(), random.Next());

                if (hashSet.Add(seed.GetHashCode()))
                    seeds[index] = seed;
                else
                    index--;
            }

            return seeds;
        }
    }
}