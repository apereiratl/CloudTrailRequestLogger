namespace ServiceStack.CloudTrail.RequestLogsFeature
{
    public static class ConfigKeys
    {
        private const string KeyPrefix = "servicestack.cloudtrail.requestlogs.";

        public static string Enabled => $"{KeyPrefix}enabled";

        public static string EnableErrorTracking => $"{KeyPrefix}errortracking.enabled";

        public static string EnableRequestBodyTracking => $"{KeyPrefix}requestbodytracking.enabled";

        public static string EnableSessionTracking => $"{KeyPrefix}sessiontracking.enabled";

        public static string EnableResponseTracking => $"{KeyPrefix}responsetracking.enabled";

        public static string RequiredRoles => $"{KeyPrefix}requiredroles";

        public static string AwsAccessKeyId => $"{KeyPrefix}aws.AccessKeyId";

        public static string AwsSecretAccessKey => $"{KeyPrefix}aws.SecretAccessKey";

        public static string Region => $"{KeyPrefix}aws.Region";

        public static string LogGroupName => $"{KeyPrefix}LogGroup";
    }
}