using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace GesEventSpike.Matching
{
    public static class MatchingHandlers
    {
        private const string KeyFormat = "matching:band-{0}:bucket-{1}";

        public static int[] GetBucketHashes(int[] signature, int bandCount)
        {
            var bandLength = signature.Length/bandCount;

            return Enumerable.Range(0, bandCount)
                .Select(bandIndex => signature
                    .Skip(bandIndex*bandLength)
                    .Take(bandLength)
                    .Aggregate((first, second) => first ^ second))
                .ToArray();
        }

        public static IEnumerable<Task> Index(string documentId, int[] bucketHashes, IDatabase database)
        {
            return bucketHashes
                .Select((bucketHash, bandIndex) => string.Format(KeyFormat, bandIndex, bucketHash))
                .Select(listKey => database.ListLeftPushAsync(listKey, documentId));
        }

        public static async Task<IEnumerable<string>> QueryBestCandidate(int[] bucketHashes, IDatabase database)
        {
            var readTasks = new Task<RedisValue[]>[bucketHashes.Length];

            for (var bandIndex = 0; bandIndex < bucketHashes.Length; bandIndex++)
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
