using Andes.Mailer.Api.Startup;
using Andes.Mailer.Common.Validation;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Options;

using FluentValidation;

namespace Andes.Mailer.Api.Configuration;

internal static class EmailsConfiguration
{
    #region Public static methods

    public static IServiceCollection AddEmails(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<AttachmentUploadOptions>, AttachmentUploadOptionsValidator>();
        services.AddOptions<AttachmentUploadOptions>()
            .Bind(configuration.GetSection(AttachmentUploadOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddSingleton<AttachmentStagingArea>();
        services.AddSingleton<AttachmentUploadChannel>();
        services.AddSingleton<AttachmentQuotaLock>();

        // Registered first so its first attempt at the containers runs before the processor's first upload.
        services.AddHostedService<BlobStorageBootstrapper>();
        services.AddHostedService<AttachmentUploadProcessor>();

        return services;
    }

    #endregion
}
