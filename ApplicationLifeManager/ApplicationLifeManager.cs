using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace ApplicationLifeManager
{
   public interface IApplicationLifeManager
   {
      bool IsAppStopping { get; set; }
      int RequestCount { get; }

      void RequestIncrease();

      void RequestDecrease();
   }

   public class ApplicationLifeManager : IApplicationLifeManager
   {
      private static long appStopRequestTime = 0;

      private readonly ILogger<ApplicationLifeManager> _Logger;
      private readonly ShutdownSettings _ShutdownSettings;
      private int requestCount = 0;
      public int RequestCount { get => requestCount; }
      public bool IsAppStopping { get; set; } = false;

      public ApplicationLifeManager(IHostApplicationLifetime hostApplicationLifetime, ILogger<ApplicationLifeManager> logger, IOptions<ShutdownSettings> options)
      {
         _ShutdownSettings = options.Value;
         hostApplicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
         hostApplicationLifetime.ApplicationStopped.Register(OnApplicationStopped);
         _Logger = logger;
      }

      public void RequestIncrease()
      {
         requestCount++;
      }

      public void RequestDecrease()
      {
         requestCount--;
      }

      private static long GetCurrentTotalMSeconds()
      {
         return DateTimeOffset.Now.ToUnixTimeMilliseconds();
      }

      private int SecondToShutdown()
      {
         return Convert.ToInt32(_ShutdownSettings.WaitTimeout.TotalSeconds * 1000 - (GetCurrentTotalMSeconds() - appStopRequestTime));
      }

      private void OnApplicationStopped()
      {
         _Logger.LogInformation("ApplicationStopped.");
      }

      private void OnApplicationStopping()
      {
         _Logger.LogInformation("SIGTERM received, waiting for pending requests ({RequestCount}) to complete.", requestCount);
         IsAppStopping = true;
         appStopRequestTime = GetCurrentTotalMSeconds();

         while (requestCount > 0 && SecondToShutdown() > 0)
         {
            Task.Delay(1000).Wait();
            _Logger.LogInformation("{SecondToShutdown} ms to shutdown. Waiting for pending requests. Pending request count: {RequestCount}. ", SecondToShutdown(), requestCount);
         }

         if (requestCount > 0 && SecondToShutdown() <= 0)
         {
            _Logger.LogError("Timeout occurred! Application will terminate with {RequestCount} pending requests.", requestCount);
         }
         else
         {
            _Logger.LogInformation("There is no pending request, application will be terminated.");
         }
      }
   }
}