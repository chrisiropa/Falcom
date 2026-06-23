using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Falcom;

public static class FalcomLoggingExtensions
{
   public static ILoggingBuilder AddFalcomLogging(this ILoggingBuilder loggingBuilder, IConfiguration configuration)
   {
      loggingBuilder.Services.AddSingleton<FalcomFileSink>(serviceProvider =>
      {
         var appSettings = serviceProvider.GetRequiredService<IOptions<Appsettings>>().Value;
         return new FalcomFileSink(appSettings.LogfilePath);
      });
      loggingBuilder.Services.AddSingleton<FalcomConsoleSink>();
      loggingBuilder.Services.AddSingleton<ILoggerProvider, FalcomLoggerProvider>();

      loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
      return loggingBuilder;
   }
}
