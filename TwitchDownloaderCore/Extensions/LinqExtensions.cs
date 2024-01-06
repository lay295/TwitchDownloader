﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchDownloaderCore.Extensions
{
    public static class LinqExtensions
    {
        public static IEnumerable<TSource> WhereOnlyIf<TSource>(this IEnumerable<TSource> enumerable, Func<TSource, bool> predicate, bool shouldFilter)
        {
            if (shouldFilter)
            {
                return enumerable.Where(predicate);
            }

            return enumerable;
        }
    }
}