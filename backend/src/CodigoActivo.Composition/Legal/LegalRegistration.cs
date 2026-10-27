using CodigoActivo.Application.Legal;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Legal;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Legal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Legal;

/// <summary>
/// Registers the legal identity of the association, read from the <c>LEGAL_*</c> settings, and the
/// store of the legal policies.
/// </summary>
internal static class LegalRegistration
{
    /// <summary>
    /// Adds the legal policy repository and the legal identity options; a name, tax identifier,
    /// address or registry longer than <see cref="LegalEntityOptions.MaxLength"/>, or an email or
    /// phone that is not valid, stops the start.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLegal(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<ILegalPolicyRepository, LegalPolicyRepository>();
        var settings = new Settings(configuration);
        services.AddValidatedOptions<LegalEntityOptions>(
            options =>
            {
                options.Name = settings.Text("LEGAL_NAME", string.Empty);
                options.TaxId = settings.Text("LEGAL_TAX_ID", string.Empty);
                options.Address = settings.Text("LEGAL_ADDRESS", string.Empty);
                options.Registry = settings.Text("LEGAL_REGISTRY", string.Empty);
                options.Email = settings.Text("LEGAL_EMAIL", string.Empty);
                options.Phone = settings.Text("LEGAL_PHONE", string.Empty);
            },
            options =>
                new[] { options.Name, options.TaxId, options.Address, options.Registry }.All(
                    value => value.Length <= LegalEntityOptions.MaxLength
                ),
            $"LEGAL_NAME, LEGAL_TAX_ID, LEGAL_ADDRESS and LEGAL_REGISTRY must not exceed {LegalEntityOptions.MaxLength} characters."
        );
        services
            .AddOptions<LegalEntityOptions>()
            .Validate(
                options =>
                    options.Email.Length == 0 || EmailAddress.Create(options.Email).IsSuccess,
                "LEGAL_EMAIL must be a single email address."
            )
            .Validate(
                options => options.Phone.Length == 0 || PhoneNumber.Create(options.Phone).IsSuccess,
                $"LEGAL_PHONE must be a phone number of {PhoneNumber.MinDigits} to {PhoneNumber.MaxDigits} digits."
            );
        return services;
    }
}
