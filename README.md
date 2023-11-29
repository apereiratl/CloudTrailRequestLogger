# ServiceStack.CloudTrail.RequestLogsFeature

[![NuGet version](https://badge.fury.io/nu/ServiceStack.CloudTrail.RequestLogsFeature.svg)](https://badge.fury.io/nu/ServiceStack.CloudTrail.RequestLogsFeature)

A ServiceStack plugin that logs requests to [AWS CloudTrail](https://aws.amazon.com/cloudtrail/) as JSON. For more details on JSON logging to CloudTrail, please review [this page](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/FilterAndPatternSyntax.html#matching-terms-json-log-events) and [this page](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/CWL_AnalyzeLogData-discoverable-fields.html).

*NB. This version is compatible with ServiceStack v8.x.

# Installing

The package is available from nuget.org

`Install-Package Servicestack.CloudTrail.RequestLogsFeature`

# Requirements

You must have an AWS account and a user with access key and secret created.

# Quick Start

In your `AppHost` class `Configure` method, add the plugin. By default configuration values are read from the registered `IAppSettings` instance. By default this will be an instance of `AppSettings`, if an alternative implementation of `IAppSettings` is to be used it must be registered prior to this plugin being registered.
Alternatively all configuration options are exposed as public properties of the feature class.

```csharp
public override void Configure(Container container)
{
    // Basic setup. All config read from AppSettings
    Plugins.Add(new CloudTrailRequestLogsFeature());

    var logger = new CloudTrailRequestLogsFeature
    {
        // required parameter
        AwsAccessKeyId = "logger_user_key_id",

        // required parameter
        AwsSecretAccessKey = "logger_secret_access_key",

        // a region code from the list: https://www.aws-services.info/regions.html
        // required parameter
        Region = "ca-central-1",
        
        // this is the name of the log group shown within the cloudtrail UI
        // required parameter
        LogGroupName = "{CompanyName}/Api",

        // this can be the individual API instance or ip address of the machine.
        // required parameter
        LogStreamName = calculatedStreamName,
        
        // add additional properties to Cloudtrail log entry.
        AppendProperties = (request, dto, response, duration) => new Dictionary<string, object> { { "NewCustomProperty", "42" } },

        // exclude specific dto types from logging
        ExcludeRequestDtoTypes = new[] { typeof(HealthCheckService.IsAlive) }, 
        
        // exclude request body logging for specific dto types
        HideRequestBodyForRequestDtoTypes = new[] { typeof(HealthCheckService.IsAlive) },
        
        // custom request logging exclusion
        SkipLogging = (request) => request.RawUrl == "/isalive"; 
    };

    this.Plugins.Add(logger);
}
```
### Configuration Options
| Property | Description | AppSettings key |
| --- | --- | --- |
| AwsAccessKeyId | AWS access key id. Required | servicestack.cloudtrail.requestlogs.aws.AccessKeyId|
| AwsSecretAccessKey | AWS secret access key. Required. | servicestack.cloudtrail.requestlogs.aws.SecretAccessKey|
| Region | AWS region. | servicestack.cloudtrail.requestlogs.aws.Region|
| LogGroupName | Log group name. Required | servicestack.cloudtrail.requestlogs.LogGroup|
| LogStreamName | Log stream name. Required | N/A |
| Enabled | Default True | servicestack.cloudtrail.requestlogs.enabled|
| EnableErrorTracking | Default True | servicestack.cloudtrail.requestlogs.errortracking.enabled|
| EnableRequestBodyTracking | Default False | servicestack.cloudtrail.requestlogs.requestbodytracking.enabled|
| EnableSessionTracking | Default False | servicestack.cloudtrail.requestlogs.sessiontracking.enabled|
| EnableResponseTracking | Default False | servicestack.cloudtrail.requestlogs.responsetracking.enabled|
| AppendProperties | Add additional properties to log | N/A|
| RawEventLogger | low evel delegate for custom logging, bypasses all other settings | responsetracking.enabled|
| Logger | Swap out cloudtrail logger for custom implementation | responsetracking.enabled|
| RequiredRoles | Restrict the runtime configuration to specific roles | servicestack.cloudtrail.requestlogs.requiredroles|
| HideRequestBodyForRequestDtoTypes | Type exclusions for body request logging | N/A|
| ExcludeRequestDtoTypes | Type exclusions for logging | N/A|
| SkipLogging | Skip logging for any custom IRequest conditions | N/A


### Request Correlation

This plugin will detect the default header `x-mac-requestid` created by [ServiceStack.Request.Correlation](https://github.com/MacLeanElectrical/servicestack-request-correlation)
and add this as a property. This is useful for tracking requests from their point of origin across multiple services

### Logging in action

Once you start your `AppHost`, every request will be now logged to cloudtrail using the default options or the options you provided.
Logging levels are colour coded and depending on your settings, the full requestDto's and even responseDto's are available to search.