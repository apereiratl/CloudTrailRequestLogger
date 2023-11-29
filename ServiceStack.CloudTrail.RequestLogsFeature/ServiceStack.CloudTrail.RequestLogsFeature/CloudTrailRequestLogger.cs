namespace ServiceStack.CloudTrail.RequestLogsFeature
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using Amazon;
    using Amazon.CloudWatchLogs;
    using Amazon.CloudWatchLogs.Model;
    using ServiceStack.Logging;
    using ServiceStack.Web;

    /// <summary>
    /// A logger to log requests to AWS CloudTrail.
    /// </summary>
    public class CloudTrailRequestLogger : IRequestLogger
    {
        private readonly CloudTrailRequestLogsFeature feature;

        private static int requestId;

        private readonly AmazonCloudWatchLogsClient cloudTrailClient;

        private bool envInit;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudTrailRequestLogger"/> class.
        /// </summary>
        /// <param name="feature">
        /// The configuration for the feature.
        /// </param>
        public CloudTrailRequestLogger(CloudTrailRequestLogsFeature feature)
        {
            this.feature = feature;
            var targetRegion = RegionEndpoint.GetBySystemName(feature.Region);
            this.cloudTrailClient = new AmazonCloudWatchLogsClient(feature.AwsAccessKeyId, feature.AwsSecretAccessKey, targetRegion);
        }

        private async Task SetupCloudTrailEnv()
        {
            if (this.envInit)
            {
                return;
            }

            var existing = await this.cloudTrailClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest { LogGroupNamePrefix = this.feature.LogGroupName }).ConfigureAwait(false);

            if (existing.LogGroups.TrueForAll(l => l.LogGroupName != this.feature.LogGroupName))
            {
                await this.cloudTrailClient.CreateLogGroupAsync(new CreateLogGroupRequest(this.feature.LogGroupName)).ConfigureAwait(false);
            }

            var existingStream = await this.cloudTrailClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest { LogGroupName = this.feature.LogGroupName }).ConfigureAwait(false);

            if (existingStream.LogStreams.Exists(x => x.LogStreamName == this.feature.LogStreamName))
            {
                return;
            }

            await this.cloudTrailClient.CreateLogStreamAsync(new CreateLogStreamRequest(this.feature.LogGroupName, this.feature.LogStreamName)).ConfigureAwait(false);

            this.envInit = true;
        }

        private async void BufferedLogEntries(CloudTrailRequestLogEntry entry)
        {
            await this.SetupCloudTrailEnv().ConfigureAwait(false);

            await this.cloudTrailClient.PutLogEventsAsync(new PutLogEventsRequest
            {
                LogStreamName = this.feature.LogStreamName,
                LogGroupName = this.feature.LogGroupName,
                LogEvents = new List<InputLogEvent>
                {
                    new InputLogEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        Message = entry.ToJson(),
                    },
                },
            }).ConfigureAwait(false);
        }

        public bool Enabled
        {
            get => this.feature.Enabled;
            set => this.feature.Enabled = value;
        }

        /// <inheritdoc/>
        public bool EnableSessionTracking
        {
            get => this.feature.EnableSessionTracking;
            set => this.feature.EnableSessionTracking = value;
        }

        /// <inheritdoc/>
        public bool EnableRequestBodyTracking
        {
            get => this.feature.EnableRequestBodyTracking;
            set => this.feature.EnableRequestBodyTracking = value;
        }

        /// <inheritdoc/>
        public Func<IRequest, bool> RequestBodyTrackingFilter { get; set; }

        /// <inheritdoc/>
        public bool EnableResponseTracking
        {
            get => this.feature.EnableResponseTracking;
            set => this.feature.EnableResponseTracking = value;
        }

        /// <inheritdoc/>
        public Func<IRequest, bool> ResponseTrackingFilter { get; set; }

        /// <inheritdoc/>
        public bool EnableErrorTracking
        {
            get => this.feature.EnableErrorTracking;
            set => this.feature.EnableErrorTracking = value;
        }

        /// <inheritdoc/>
        public string[] RequiredRoles
        {
            get => this.feature.RequiredRoles?.ToArray();
            set => this.feature.RequiredRoles = value?.ToList();
        }

        /// <inheritdoc/>
        public Type[] ExcludeRequestDtoTypes
        {
            get => this.feature.ExcludeRequestDtoTypes?.ToArray();
            set => this.feature.ExcludeRequestDtoTypes = value?.ToList();
        }

        public Type[] HideRequestBodyForRequestDtoTypes
        {
            get => this.feature.HideRequestBodyForRequestDtoTypes?.ToArray();
            set => this.feature.HideRequestBodyForRequestDtoTypes = value?.ToList();
        }

        public Type[] ExcludeResponseTypes { get; set; }

        /// <summary>
        /// Input: request, requestDto, response, requestDuration
        /// Output: List of Properties to append to the Log entry
        /// </summary>
        public CloudTrailRequestLogsFeature.PropertyAppender AppendProperties
        {
            get => this.feature.AppendProperties;
            set => this.feature.AppendProperties = value;
        }

        public void Log(IRequest request, object requestDto, object response, TimeSpan requestDuration)
        {
            try
            {
                // bypasses all flags to run raw log event delegate if configured
                this.feature.RawEventLogger?.Invoke(request, requestDto, response, requestDuration);

                // if logging disabled
                if (!this.Enabled)
                {
                    return;
                }

                // check any custom filter
                if (this.feature.SkipLogging?.Invoke(request) == true)
                {
                    return;
                }

                // skip logging any dto exclusion types set
                var requestType = requestDto?.GetType();
                if (requestType != null && this.ExcludeRequestType(requestType))
                {
                    return;
                }

                var entry = this.CreateEntry(request, requestDto, response, requestDuration, requestType);
                this.BufferedLogEntries(entry);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(typeof(CloudTrailRequestLogger)).Error("CloudTrailRequestLogger threw unexpected exception", ex);
            }
        }

        /// <inheritdoc/>
        public List<RequestLogEntry> GetLatestLogs(int? take)
        {
            // use cloudtrail ui for reading logs
            throw new NotSupportedException($"use aws cloudtrail ui to read logs");
        }

        private CloudTrailRequestLogEntry CreateEntry(IRequest request, object requestDto, object response, TimeSpan requestDuration, Type requestType)
        {
            var requestLogEntry = new CloudTrailRequestLogEntry
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                MessageTemplate = "HTTP {HttpMethod} {PathInfo} responded {StatusCode} in {ElapsedMilliseconds}ms",
            };
            requestLogEntry.Properties.Add("IsRequestLog", "True"); // Used for filtering requests easily
            requestLogEntry.Properties.Add("SourceContext", "ServiceStack.CloudTrail.RequestLogsFeature");
            requestLogEntry.Properties.Add("ElapsedMilliseconds", requestDuration.TotalMilliseconds);
            requestLogEntry.Properties.Add("RequestCount", Interlocked.Increment(ref requestId).ToString());
            requestLogEntry.Properties.Add("ServiceName", HostContext.AppHost.ServiceName);

            if (request != null)
            {
                requestLogEntry.Properties.Add("HttpMethod", request.Verb);
                requestLogEntry.Properties.Add("AbsoluteUri", request.AbsoluteUri);
                requestLogEntry.Properties.Add("PathInfo", request.PathInfo);
                requestLogEntry.Properties.Add("IpAddress", request.UserHostAddress);
                requestLogEntry.Properties.Add("ForwardedFor", request.Headers[HttpHeaders.XForwardedFor]);
                requestLogEntry.Properties.Add("Referer", request.Headers[HttpHeaders.Referer]);
                requestLogEntry.Properties.Add("Session", this.EnableSessionTracking ? request.GetSession(false) : null);
                requestLogEntry.Properties.Add("Items", request.Items.WithoutDuplicates());
                requestLogEntry.Properties.Add("StatusCode", request.Response?.StatusCode);
                requestLogEntry.Properties.Add("StatusDescription", request.Response?.StatusDescription);
                requestLogEntry.Properties.Add("ResponseStatus", request.Response?.GetResponseStatus());
            }

            var isClosed = request.Response.IsClosed;
            if (!isClosed)
            {
                requestLogEntry.Properties.Add("UserAuthId", request.GetItemOrCookie(HttpHeaders.XUserAuthId));
                requestLogEntry.Properties.Add("SessionId", request.GetSessionId());
            }

            if (this.HideRequestBodyForRequestDtoTypes != null && requestType != null && !this.HideRequestBodyForRequestDtoTypes.Contains(requestType))
            {
                requestLogEntry.Properties.Add("RequestDto", requestDto);
                if (request != null)
                {
                    if (!isClosed)
                    {
                        requestLogEntry.Properties.Add("FormData", request.FormData.ToDictionary());
                    }

                    if (this.EnableRequestBodyTracking)
                    {
                        requestLogEntry.Properties.Add("RequestBody", request.GetRawBody());
                    }
                }
            }

            if (!response.IsErrorResponse())
            {
                if (this.EnableResponseTracking)
                {
                    requestLogEntry.Properties.Add("ResponseDto", response);
                }
            }
            else if (this.EnableErrorTracking)
            {
                if (response is IHttpError errorResponse)
                {
                    requestLogEntry.Level = errorResponse.StatusCode >= HttpStatusCode.BadRequest
                                            && errorResponse.StatusCode < HttpStatusCode.InternalServerError
                                                ? "Warning"
                                                : "Error";
                    requestLogEntry.Properties["StatusCode"] = (int)errorResponse.StatusCode;
                    requestLogEntry.Properties.Add("ErrorCode", errorResponse.ErrorCode);
                    requestLogEntry.Properties.Add("ErrorMessage", errorResponse.Message);
                    requestLogEntry.Properties.Add("StackTrace", errorResponse.StackTrace);
                }

                if (response is Exception ex)
                {
                    if (ex.InnerException != null)
                    {
                        requestLogEntry.Exception = ex.InnerException.ToString();
                        requestLogEntry.Properties.Add("ExceptionSource", ex.InnerException.Source);
                        requestLogEntry.Properties.Add("ExceptionData", ex.InnerException.Data);
                    }
                    else
                    {
                        requestLogEntry.Exception = ex.ToString();
                    }
                }
            }

            if (this.AppendProperties != null)
            {
                foreach (var kvPair in AppendProperties?.Invoke(request, requestDto, response, requestDuration).Safe())
                {
                    requestLogEntry.Properties.GetOrAdd(kvPair.Key, key => kvPair.Value);
                }
            }

            foreach (var header in request.Headers.ToDictionary())
            {
                if (!requestLogEntry.Properties.ContainsValue(header.Value))
                {
                    requestLogEntry.Properties.Add($"Header-{header.Key}", header.Value);
                }
            }

            return requestLogEntry;
        }

        protected bool ExcludeRequestType(Type requestType)
        {
            return ExcludeRequestDtoTypes != null
                   && requestType != null
                   && this.ExcludeRequestDtoTypes.Contains(requestType);
        }

        /// <inheritdoc/>
        public bool LimitToServiceRequests { get; set; }

        /// <inheritdoc/>
        public Func<IRequest, bool> SkipLogging { get; set; }

        /// <inheritdoc/>
        public Action<IRequest, RequestLogEntry> RequestLogFilter { get; set; }

        /// <inheritdoc/>
        public Func<object, bool> IgnoreFilter { get; set; }

        /// <inheritdoc/>
        public Func<DateTime> CurrentDateFn { get; set; } = () => DateTime.UtcNow;
    }
}
