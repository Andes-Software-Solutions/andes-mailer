using Andes.Mailer.Api.Startup;
using Andes.Mailer.Common.Validation;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Emails.Sending;
using Andes.Mailer.Service.Emails.Templates;
using Andes.Mailer.Service.Emails.Templates.Attachments;
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
        services.AddSingleton<IValidator<EmailSendingOptions>, EmailSendingOptionsValidator>();
        services.AddOptions<EmailSendingOptions>()
            .Bind(configuration.GetSection(EmailSendingOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailSendService, EmailSendService>();
        services.AddScoped<IMailboxClient, GraphMailboxClient>();
        services.AddSingleton<EmailDispatchSignal>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<ITemplateAttachmentService, TemplateAttachmentService>();
        services.AddSingleton<AttachmentLifecycle>();
        services.AddSingleton<AttachmentStagingArea>();
        services.AddSingleton<AttachmentUploadChannel>();
        services.AddSingleton<AttachmentQuotaLock>();

        // Registered first so its first attempt at the containers runs before the processor's first upload.
        services.AddHostedService<BlobStorageBootstrapper>();
        services.AddHostedService<AttachmentUploadProcessor>();

        // Always registered; it returns at once when EmailSending:Enabled is false.
        services.AddHostedService<EmailDispatchProcessor>();

        return services;
    }

    #endregion
}
