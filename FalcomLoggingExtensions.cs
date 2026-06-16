using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Falcom;

public static class FalcomLoggingExtensions
{
   public static ILoggingBuilder AddFalcomLogging(this ILoggingBuilder loggingBuilder, IConfiguration configuration)
   {
      loggingBuilder.Services.AddSingleton<ILoggerProvider, FalcomLoggerProvider>(serviceProvider =>
      {
         var appSettings = serviceProvider.GetRequiredService<IOptions<Appsettings>>().Value;
         return new FalcomLoggerProvider(appSettings.LogfilePath);
      });

      loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
      return loggingBuilder;
   }
}
