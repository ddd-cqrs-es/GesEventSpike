using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace GesEventSpike.Matching
{
    public static class MatchingHandlers
    {
        private const string KeyFormat = "matching:band-{0}:bucket-{1}";
        private const int BandCount = 32;

        public static int[] GetBucketHashes(int[] signature)
        {
            var bandLength = signature.Length/BandCount;

            return Enumerable.Range(0, BandCount)
                .Select(bandIndex => signature
                    .Skip(bandIndex*bandLength)
                    .Take(bandLength)
                    .Aggregate((first, second) => first ^ second))
                .ToArray();
        }

        public static IEnumerable<Task> Index(string documentId, int[] bucketHashes, IDatabase database)
        {
            for (var bandIndex = 0; bandIndex < BandCount; bandIndex++)
            {
                var bucketHash = bucketHashes[bandIndex];
                var listKey = string.Format(KeyFormat, bandIndex, bucketHash);

                yield return database.ListLeftPushAsync(listKey, documentId);
            }
        }

        public static async Task<IEnumerable<string>> QueryBestCandidate(int[] bucketHashes, IDatabase database)
        {
            var readTasks = new Task<RedisValue[]>[BandCount];

            for (var bandIndex = 0; bandIndex < BandCount; bandIndex++)
            {
                var bucketHash = bucketHashes[bandIndex];
                var listKey = string.Format(KeyFormat, bandIndex, bucketHash);

                readTasks[bandIndex] = database.ListRangeAsync(listKey);
            }

            return await Task.WhenAll(readTasks).ContinueWith(task =>
            {
                var candidates = task.Result;

                var bestCandidates = candidates
                    .SelectMany(bandCandidates => bandCandidates)
                    .GroupBy(id => id)
                    .GroupBy(keyIs => keyIs.Count())
                    .OrderByDescending(by => by.Key)
                    .SelectMany(byCount => byCount.Select(byId => byId.Key.ToString()))
                    .Take(1)
                    .ToArray();

                return bestCandidates;
            });
        }
    }
}
