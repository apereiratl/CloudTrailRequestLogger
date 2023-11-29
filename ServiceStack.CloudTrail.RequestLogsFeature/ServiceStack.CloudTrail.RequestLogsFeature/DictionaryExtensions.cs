namespace ServiceStack.CloudTrail.RequestLogsFeature
{
    using System;
    using System.Collections.Generic;

    public static class DictionaryExtensions
    {
        public static V GetOrAdd<K, V>(this SortedDictionary<K, V> map, K key, Func<K, V> createFn)
        {
            lock (map)
            {
                if (!map.TryGetValue(key, out var val))
                {
                    map[key] = val = createFn(key);
                }

                return val;
            }
        }
    }
}
