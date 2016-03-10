namespace GesEventSpike.ConsoleHost.Scripts
{
    internal static class ScriptContents
    {
        internal static string CreateSchema => ResourceCache.GetString(ScriptNamespace, nameof(CreateSchema), DotSql);
        internal static string PurgeData => ResourceCache.GetString(ScriptNamespace, nameof(PurgeData), DotSql);
    
        private const string DotSql = ".sql";

        private static readonly string
            ScriptNamespace = typeof (ScriptContents).Namespace;
    }
}