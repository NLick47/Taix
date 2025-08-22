using System;
using System.Collections.Generic;
using System.Threading;

namespace UI.Extensions;

public static class ThreadSafeRandom
{
    [ThreadStatic] private static Random Local;

    public static Random ThisThreadsRandom => Local ??
                                              (Local = new Random(unchecked(Environment.TickCount * 31 +
                                                                            Thread.CurrentThread.ManagedThreadId)));
}

public static class ListExtensions
{
    /// <summary>
    ///     随机打乱该List
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void Shuffle<T>(this IList<T> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
            var value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}