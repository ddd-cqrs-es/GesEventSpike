using System;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace GesEventSpike.Matching
{
    public static class MatchingHandlers
    {
        private static readonly DeterministicGuid EssenceId = new DeterministicGuid(new Guid("16BC1390-AFA1-4987-90EF-57C2B2029F53"));

        private const string
            KeyFormat = "matching:{0}:{1}";

        public static async Task<string> GetOrCreateEssence(DemographicsDocument document, IDatabaseAsync database)
        {
            var keys = GetKeys(document);

            var results = await Task.WhenAll(keys.Select(key => database.StringGetAsync(key)).ToArray());

            var maybeMatches = results
                .Zip(keys, (value, key) => new {Key = key, Value = value})
                .ToLookup(x => x.Value.HasValue);
            
            var essenceId = maybeMatches[true]
                .Select(redisValue => (string)redisValue.Value)
                .GroupBy(_ => _)
                .OrderByDescending(valueGroup => valueGroup.Count())
                .Select(valueGroup => valueGroup.Key)
                .FirstOrDefault();

            essenceId = essenceId ?? EssenceId.Create(document.DocumentId).ToString("N");

            var unmatchedKeys = maybeMatches[false].Select(x => x.Key).ToArray();

            await Task.WhenAll(unmatchedKeys.Select(key => database.StringSetAsync(key, essenceId)));

            return essenceId;
        }

        private static string[] GetKeys(DemographicsDocument document)
        {
            var demographicsToken = new[] { document.LastName, document.BirthDate.ToString("yyyyMMdd") }
                .Select(value => value.ToLower())
                .StringJoin("-");

            return new[]
            {
                new object[] {"record", document.RecordId.ToLower()},
                new object[] {"session", document.SessionId.ToLower()},
                new object[] {"demographics", demographicsToken}
            }
                .Select(formatArguments => string.Format(KeyFormat, formatArguments))
                .ToArray();
        }
    }
}
