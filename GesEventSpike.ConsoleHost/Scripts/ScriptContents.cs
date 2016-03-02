namespace GesEventSpike.ConsoleHost.Scripts
{
    internal static class ScriptContents
    {
        private static readonly string
            ScriptNamespace = typeof (ScriptContents).Namespace,
            DotSql = ".sql";

        internal static string CreateSchema => ResourceCache.GetString(ScriptNamespace, nameof(CreateSchema), DotSql);
    }
}