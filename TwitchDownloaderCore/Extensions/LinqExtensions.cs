namespace TwitchDownloaderCore.Extensions
{
    public static class LinqExtensions
    {
        extension<TSource>(IEnumerable<TSource> enumerable)
        {
            public IEnumerable<TSource> WhereOnlyIf(Func<TSource, bool> predicate, bool shouldFilter)
            {
                if (shouldFilter)
                {
                    return enumerable.Where(predicate);
                }

                return enumerable;
            }
        }
    }
}