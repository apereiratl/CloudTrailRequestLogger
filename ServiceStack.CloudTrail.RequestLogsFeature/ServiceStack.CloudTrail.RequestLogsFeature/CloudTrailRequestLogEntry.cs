namespace ServiceStack.CloudTrail.RequestLogsFeature
{
    using System.Collections.Generic;

    /// <summary>
    /// A log entry added by the IRequestLogger
    /// </summary>
    public class CloudTrailRequestLogEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CloudTrailRequestLogEntry"/> class.
        /// </summary>
        public CloudTrailRequestLogEntry()
        {
            this.MessageTemplate = "Servicestack CloudTrailRequestLogsFeature";
            this.Properties = new SortedDictionary<string, object>();
            this.Level = "Debug";
        }

        public string Timestamp { get; set; }

        public string Level { get; set; }

        public SortedDictionary<string, object> Properties { get; }

        public string MessageTemplate { get; set; }

        public string Exception { get; set; }
    }
}