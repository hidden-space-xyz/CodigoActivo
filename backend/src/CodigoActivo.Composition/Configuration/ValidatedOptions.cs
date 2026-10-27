using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodigoActivo.Composition.Configuration;

/// <summary>
/// Registers options read from configuration and checked when the host starts, so a bad setting
/// stops the application before it serves anything, and options fixed in code that no setting can
/// change.
/// </summary>
internal static class ValidatedOptions
{
    /// <summary>
    /// Adds options that keep the values written in their class, out of reach of configuration,
    /// and lets consumers take the options object itself as well as <see cref="IOptions{TOptions}"/>.
    /// </summary>
    /// <typeparam name="TOptions">Options added.</typeparam>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddFixedOptions<TOptions>(this IServiceCollection services)
        where TOptions : class
    {
        services.AddOptions<TOptions>();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TOptions>>().Value);
        return services;
    }

    /// <summary>
    /// Adds options built from configuration, checks them on start and lets consumers take the
    /// options object itself as well as <see cref="IOptions{TOptions}"/>.
    /// </summary>
    /// <typeparam name="TOptions">Options added.</typeparam>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configure">Fills the options from configuration; throws on an unusable setting.</param>
    /// <param name="validate">
    /// Checks the filled options, such as a required setting being present; null when
    /// <paramref name="configure"/> checks everything.
    /// </param>
    /// <param name="failureMessage">
    /// Message reported when <paramref name="validate"/> fails; null for a generic one naming the
    /// options type.
    /// </param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        Action<TOptions> configure,
        Func<TOptions, bool>? validate = null,
        string? failureMessage = null
    )
        where TOptions : class
    {
        var builder = services.AddOptions<TOptions>().Configure(configure);
        if (validate is not null)
        {
            builder.Validate(validate, failureMessage ?? $"{typeof(TOptions).Name} is invalid.");
        }

        builder.ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TOptions>>().Value);
        return services;
    }
}
