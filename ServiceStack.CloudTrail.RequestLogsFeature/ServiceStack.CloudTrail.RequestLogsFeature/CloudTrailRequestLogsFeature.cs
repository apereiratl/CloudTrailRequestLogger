namespace ServiceStack.CloudTrail.RequestLogsFeature
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceStack.Admin;
    using ServiceStack.CloudTrail.RequestLogsFeature.Validators;
    using ServiceStack.Configuration;
    using ServiceStack.FluentValidation;
    using ServiceStack.Logging;
    using ServiceStack.Web;

    public class CloudTrailRequestLogsFeature : IPlugin
    {
        private readonly ILog log = LogManager.GetLogger(typeof(CloudTrailRequestLogsFeature));
        private readonly IAppSettings appSettings;
        private readonly ConfigValidator configValidator = new ConfigValidator();

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudTrailRequestLogsFeature"/> class.
        /// </summary>
        public CloudTrailRequestLogsFeature()
        {
            appSettings ??= ServiceStackHost.Instance.AppSettings;

            if (this.log.IsDebugEnabled)
            {
                this.log.Debug($"Using {appSettings.GetType().Name} appSettings for appSettings provider");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudTrailRequestLogsFeature"/> class.
        /// </summary>
        /// <param name="settings">
        /// The <see cref="IAppSettings"/> instance.
        /// </param>
        public CloudTrailRequestLogsFeature(IAppSettings settings)
        {
            this.appSettings = settings.ThrowIfNull(nameof(settings));

            if (this.log.IsDebugEnabled)
            {
                this.log.Debug($"Using {this.appSettings.GetType().Name} appSettings for appSettings provider");
            }
        }

        /// <summary>
        /// Excludes requests for specific DTO types from logging, ignores RequestLog requests by default
        /// </summary>
        public IEnumerable<Type> ExcludeRequestDtoTypes { get; set; } = new List<Type>(new[] { typeof(RequestLogs) });

        /// <summary>B
        /// Exclude request body for specific DTO types from logging, ignores authentication and registration dtos by default
        /// </summary>
        public IEnumerable<Type> HideRequestBodyForRequestDtoTypes { get; set; } =
            new List<Type>(new[] { typeof(Authenticate), typeof(Register) });

        /// <summary>
        /// Restrict access to the runtime log settings 
        /// </summary>
        public List<string> RequiredRoles
        {
            get => this.appSettings.GetList(ConfigKeys.RequiredRoles)?.ToList();
            set => this.appSettings.Set(ConfigKeys.RequiredRoles, string.Join(",", value));
        }

        /// <summary>
        /// Gets or sets a value indicating whether to turn logging on and off, defaults to true
        /// </summary>
        public bool Enabled
        {
            get => this.appSettings.Get(ConfigKeys.Enabled, true);
            set => this.appSettings.Set(ConfigKeys.Enabled, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to log errors, defaults to true
        /// </summary>
        public bool EnableErrorTracking
        {
            get => this.appSettings.Get(ConfigKeys.EnableErrorTracking, true);
            set => this.appSettings.Set(ConfigKeys.EnableErrorTracking, value);
        }

        /// <summary>
        /// Log request bodies, defaults to false
        /// </summary>
        public bool EnableRequestBodyTracking
        {
            get => this.appSettings.Get(ConfigKeys.EnableRequestBodyTracking, false);
            set => this.appSettings.Set(ConfigKeys.EnableRequestBodyTracking, value);
        }

        /// <summary>
        /// Log session details, defaults to false
        /// </summary>
        public bool EnableSessionTracking
        {
            get => this.appSettings.Get(ConfigKeys.EnableSessionTracking, false);
            set => this.appSettings.Set(ConfigKeys.EnableSessionTracking, value);
        }

        /// <summary>
        /// Log responses, defaults to false
        /// </summary>
        public bool EnableResponseTracking
        {
            get => this.appSettings.Get(ConfigKeys.EnableResponseTracking, false);
            set => this.appSettings.Set(ConfigKeys.EnableResponseTracking, value);
        }

        public string AwsAccessKeyId
        {
            get => this.appSettings.Get(ConfigKeys.AwsAccessKeyId, string.Empty);
            set => this.appSettings.Set(ConfigKeys.AwsAccessKeyId, value);
        }

        public string AwsSecretAccessKey
        {
            get => this.appSettings.Get(ConfigKeys.AwsSecretAccessKey, string.Empty);
            set => this.appSettings.Set(ConfigKeys.AwsSecretAccessKey, value);
        }

        public string Region
        {
            // RegionEndpoint.GetBySystemName(appSettings.Get<string>(ConfigKeys.Region))
            get => this.appSettings.Get<string>(ConfigKeys.Region);
            set => this.appSettings.Set<string>(ConfigKeys.Region, value);
        }

        public string LogGroupName
        {
            get => this.appSettings.Get<string>(ConfigKeys.LogGroupName);
            set => this.appSettings.Set<string>(ConfigKeys.LogGroupName, value);
        }

        /// <summary>
        /// Low level request filter for logging, return true to skip logging the request
        /// </summary>
        public Func<IRequest, bool> SkipLogging { get; set; }

        /// <summary>
        /// Append custom properties to all log entries
        /// </summary>
        public PropertyAppender AppendProperties { get; set; }

        /// <summary>
        /// Lowest level access to customised logging, executes before any other logging settings
        /// </summary>
        public RawLogEvent RawEventLogger { get; set; }

        private IRequestLogger logger;

        /// <summary>
        /// Sets the <see cref="CloudTrailRequestLogger"/> by default, override with a custom implementation of <see cref="IRequestLogger"/>
        /// </summary>
        public IRequestLogger Logger
        {
            get => logger ?? new CloudTrailRequestLogger(this);
            set => logger = value;
        }

        /// <summary>
        /// Gets or sets the name of the log stream for cloudtrail.
        /// </summary>
        public string LogStreamName { get; set; }

        /// <summary>
        /// Low level delegate for appending custom properties to a log entry
        /// </summary>
        /// <param name="request"></param>
        /// <param name="requestDto"></param>
        /// <param name="response"></param>
        /// <param name="soapDuration"></param>
        public delegate Dictionary<string, object> PropertyAppender(
            IRequest request,
            object requestDto,
            object response,
            TimeSpan soapDuration);

        /// <summary>
        /// Low level delegate for customised logging
        /// </summary>
        /// <param name="request"></param>
        /// <param name="requestDto"></param>
        /// <param name="response"></param>
        /// <param name="requestDuration"></param>
        public delegate void RawLogEvent(
            IRequest request,
            object requestDto,
            object response,
            TimeSpan requestDuration);

        /// <summary>
        /// Registers the plugin with the apphost
        /// </summary>
        /// <param name="appHost"></param>
        public void Register(IAppHost appHost)
        {
            configValidator.ValidateAndThrow(this);

            ConfigureRequestLogger(appHost);

            if (EnableRequestBodyTracking)
            {
                appHost.PreRequestFilters.Insert(0, (httpReq, httpRes) =>
                {
                    httpReq.UseBufferedStream = true;
                });
            }
        }

        private void ConfigureRequestLogger(IAppHost appHost)
        {
            var requestLogger = this.Logger;
            requestLogger.EnableSessionTracking = this.EnableSessionTracking;
            requestLogger.EnableResponseTracking = this.EnableResponseTracking;
            requestLogger.EnableRequestBodyTracking = this.EnableRequestBodyTracking;
            requestLogger.EnableErrorTracking = this.EnableErrorTracking;
            requestLogger.ExcludeRequestDtoTypes = this.ExcludeRequestDtoTypes.ToArray();
            requestLogger.HideRequestBodyForRequestDtoTypes = this.HideRequestBodyForRequestDtoTypes.ToArray();
            requestLogger.SkipLogging = this.SkipLogging;
            appHost.Register(requestLogger);
        }
    }
}
