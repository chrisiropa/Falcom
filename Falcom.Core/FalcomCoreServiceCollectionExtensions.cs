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
      services.AddSingleton<FalcomKranLiveStatusService>();
      services.AddSingleton<FalcomKranLiveSignalRClient>();
      services.AddHostedService<FalcomKranLiveSignalRServer>();
      services.AddSingleton<AktuelleFahrtRepository>();
      services.AddSingleton<BunkerMaterialRepository>();
      services.AddSingleton<KranPositionenRepository>();
      services.AddSingleton<MaterialEigenschaftenRepository>();
      services.AddSingleton<MaterialEigenschaftenAnforderungRepository>();
      services.AddHostedService<DatabaseOrderPoller>();
      services.AddHostedService<Worker>();

      return services;
   }
}



