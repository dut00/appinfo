using Dut00.AppInfo.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dut00.AppInfo;

/// <summary>
/// Adds fields from the core package to the <c>/appinfo</c> response.
/// </summary>
public static class AppInfoBuilderExtensions
{
    /// <summary>
    /// Adds the <c>Owner</c> field.
    /// </summary>
    /// <remarks>
    /// Safe to call more than once: the field is added once and every <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="configure">Sets <see cref="OwnerOptions.Owner"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithOwner(this IAppInfoBuilder builder, Action<OwnerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, OwnerAppInfoContributor>());
        return builder;
    }

    /// <summary>
    /// Adds a custom field with a constant value.
    /// </summary>
    /// <remarks>
    /// The value is written as-is, so it must not contain secrets.
    /// To write a constant <see langword="null"/>, cast it: <c>WithProperty("Key", (object?)null)</c>.
    /// A plain <see langword="null"/> binds to the factory overload and throws.
    /// Delegates are rejected: a lambda such as <c>() =&gt; DateTime.UtcNow</c> binds here, not to the factory overload,
    /// and can't be serialized. Use <c>sp =&gt; DateTime.UtcNow</c> instead.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="key">The key, emitted as-is unless a naming policy is configured.</param>
    /// <param name="value">Any JSON-serializable value, or <see langword="null"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is a delegate.</exception>
    public static IAppInfoBuilder WithProperty(this IAppInfoBuilder builder, string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (value is Delegate)
        {
            throw new ArgumentException(
                "A delegate can't be used as a constant value. " +
                "For a value computed on every request, pass a Func<IServiceProvider, object?>, for example 'sp => DateTime.UtcNow'.",
                nameof(value));
        }

        return AddDelegateContributor(builder, key, _ => value);
    }

    /// <summary>
    /// Adds a custom field whose value is computed on every request.
    /// </summary>
    /// <remarks>
    /// The factory receives the request's service provider, so it may resolve scoped services.
    /// The value is written as-is, so it must not contain secrets.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="key">The key, emitted as-is unless a naming policy is configured.</param>
    /// <param name="factory">Computes the value from the request's service provider.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithProperty(this IAppInfoBuilder builder, string key, Func<IServiceProvider, object?> factory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        return AddDelegateContributor(builder, key, factory);
    }

    /// <summary>
    /// Registers a custom <see cref="IAppInfoContributor"/>. This is the hook extension packages build their <c>With*</c> methods on.
    /// </summary>
    /// <remarks>
    /// The contributor is registered as scoped, so it may depend on scoped services. Registering the same type
    /// more than once has no effect.
    /// </remarks>
    /// <typeparam name="T">The contributor type.</typeparam>
    /// <param name="builder">The AppInfo builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithContributor<T>(this IAppInfoBuilder builder)
        where T : class, IAppInfoContributor
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAppInfoContributor, T>());
        return builder;
    }

    // Added with Add, not TryAddEnumerable: every delegate contributor shares one implementation type,
    // so TryAddEnumerable would keep only the first WithProperty call.
    private static IAppInfoBuilder AddDelegateContributor(IAppInfoBuilder builder, string key, Func<IServiceProvider, object?> factory)
    {
        builder.Services.AddSingleton<IAppInfoContributor>(new DelegateAppInfoContributor(key, factory));
        return builder;
    }
}
