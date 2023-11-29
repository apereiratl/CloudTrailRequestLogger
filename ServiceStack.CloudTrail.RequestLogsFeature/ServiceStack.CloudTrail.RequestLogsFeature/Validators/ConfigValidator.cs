namespace ServiceStack.CloudTrail.RequestLogsFeature.Validators
{
    using Amazon;
    using ServiceStack.FluentValidation;

    internal class ConfigValidator : AbstractValidator<CloudTrailRequestLogsFeature>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigValidator"/> class.
        /// </summary>
        public ConfigValidator()
        {
            this.RuleFor(cs => cs.AwsAccessKeyId)
                .NotEmpty()
                .WithMessage("Aws access key id is required.");

            this.RuleFor(cs => cs.AwsSecretAccessKey)
                .NotEmpty()
                .WithMessage("Aws secret access key is required.");

            this.RuleFor(cs => cs.Region)
                .NotEmpty()
                .Must(x => RegionEndpoint.GetBySystemName(x) != null)
                .WithMessage("Aws region is invalid.");
        }
    }
}
