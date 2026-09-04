using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Seg;
using Segusum.WebClient;

namespace Segusum.AspNetCore;

/// <summary>
/// Configurazione minima che consente a un gioco di fornire i propri World
/// senza dover scrivere controller ASP.NET personalizzati.
/// </summary>
public sealed class SegusumOptions
{
    public Func<string, bool, WorldBase>? WorldFactory { get; set; }
    public string GameTitle { get; set; } = "Segusum game";
    public string InventoryIconsPath { get; set; } = "_content/Segusum.WebClient/assets/icons";
    public string GameAssetPrefix { get; set; } = "";
    public string Credits { get; set; } = "A game made with Segusum.";
}

public interface ISegusumWorldFactory
{
    WorldBase Create(string language, bool tutorialMode);
}

internal sealed class ConfiguredSegusumWorldFactory : ISegusumWorldFactory
{
    private readonly Func<string, bool, WorldBase> factory;

    public ConfiguredSegusumWorldFactory(Func<string, bool, WorldBase> factory)
    {
        this.factory = factory;
    }

    public WorldBase Create(string language, bool tutorialMode)
        => factory(language, tutorialMode);
}

public static class SegusumServiceCollectionExtensions
{
    /// <summary>
    /// Registra Segusum usando direttamente la factory del gioco.
    /// </summary>
    public static IServiceCollection AddSegusum(
        this IServiceCollection services,
        Func<string, bool, WorldBase> worldFactory)
    {
        ArgumentNullException.ThrowIfNull(worldFactory);

        return services.AddSegusum(options => options.WorldFactory = worldFactory);
    }

    public static IServiceCollection AddSegusum(
        this IServiceCollection services,
        Action<SegusumOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SegusumOptions();
        configure(options);

        if (options.WorldFactory is null)
        {
            throw new InvalidOperationException(
                "Segusum richiede una WorldFactory. Configurala in Program.cs con il World del gioco.");
        }

        services.AddSingleton<ISegusumWorldFactory>(
            new ConfiguredSegusumWorldFactory(options.WorldFactory));
        services.AddSingleton(options);
        services.AddSingleton<SegusumSessionStore>();

        // Il package possiede il controller standard: il gioco non deve
        // conoscere il tipo concreto soltanto per configurare MVC.
        services.AddControllersWithViews()
            .AddNewtonsoftJson()
            .AddApplicationPart(typeof(SegusumController).Assembly);

        return services;
    }
}
