namespace ServiceStack.CloudTrail.RequestLogsFeature
{
    using System.Collections.Generic;

    public static class LogExtensions
    {
        public static Dictionary<string, object> WithoutDuplicates(this Dictionary<string, object> items)
        {
            items.Remove("__session");
            items.Remove("_requestDurationStopwatch");
            items.Remove("x-mac-requestId");
            return items;
        }
    }
}