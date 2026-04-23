using Microsoft.Extensions.DependencyInjection;
using Soenneker.Kiota.Util.Registrars;
using Soenneker.Managers.Runners.Registrars;
using Soenneker.Netlify.Runners.OpenApiClient.Utils;
using Soenneker.Netlify.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.OpenApi.Converter.Registrars;
using Soenneker.Utils.File.Download.Registrars;
using Soenneker.Utils.Yaml.Registrars;

namespace Soenneker.Netlify.Runners.OpenApiClient;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
                .AddRunnersManagerAsSingleton()
                .AddFileDownloadUtilAsSingleton()
                .AddKiotaUtilAsSingleton()
                .AddYamlUtilAsSingleton()
                .AddOpenApiConverterAsSingleton();

        return services;
    }
}
