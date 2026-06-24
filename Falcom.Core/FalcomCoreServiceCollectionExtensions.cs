using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Falcom;

public static class FalcomCoreServiceCollectionExtensions
{
   public static IServiceCollection AddFalcomCore(
      this IServiceCollection services,
      IConfiguration configuration)
   {
      services.Configure<Appsettings>(configuration.GetSection("Appsettings"));
      services.AddSingleton<ConfigManager>();
      services.AddSingleton<FalcomRuntimeStatus>();
      services.AddSingleton<Parameter>();
      services.AddSingleton<Lager>();
      services.AddSingleton<OPC_Client_Crane>();
      services.AddSingleton<FalcomEventQueue>();
      services.AddSingleton<WatchdogSender>();
      services.AddSingleton<AktuelleFahrtRepository>();
      services.AddHostedService<DatabaseOrderPoller>();
      services.AddHostedService<Worker>();

      return services;
   }
}
