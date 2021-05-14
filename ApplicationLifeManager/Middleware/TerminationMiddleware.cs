using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace ApplicationLifeManager.Middleware
{
   public class TerminationMiddleware
   {
      /// <summary>
      /// </summary>
      private readonly RequestDelegate _Next;

      private readonly IApplicationLifeManager _ApplicationLifeManager;
      private readonly ILogger _Logger;
      private readonly ShutdownSettings _ShutdownSettings;
      private static readonly object _lock = new object();

      public TerminationMiddleware(RequestDelegate next, IApplicationLifeManager applicationLifeManager, ILogger<TerminationMiddleware> logger, IOptions<ShutdownSettings> options)
      {
         _ShutdownSettings = options.Value;
         _Next = next;
         _ApplicationLifeManager = applicationLifeManager;
         _Logger = logger;
      }

      public async Task Invoke(HttpContext httpContext)
      {
         if (ShouldIgnorePath(httpContext))
         {
            await _Next.Invoke(httpContext);
         }
         else if (_ApplicationLifeManager.IsAppStopping)
         {
            await HandleNewRequestDuringTermination(httpContext);
         }
         else
         {
            lock (_lock)
            {
               _ApplicationLifeManager.RequestIncrease();
            }

            await _Next.Invoke(httpContext);

            lock (_lock)
            {
               _ApplicationLifeManager.RequestDecrease();
            }
         }
      }

      private bool ShouldIgnorePath(HttpContext httpContext)
      {
         foreach (var ignoredPath in _ShutdownSettings.IgnorePaths)
         {
            if (httpContext.Request.Path.StartsWithSegments(ignoredPath))
            {
               return true;
            }
         }

         return false;
      }

      private async Task HandleNewRequestDuringTermination(HttpContext httpContext)
      {
         _Logger.LogInformation("Request received during termination process. Sending response as service unavailable (HTTP 503).");

         httpContext.Response.StatusCode = 503;

         await httpContext.Response.WriteAsync("503 - Service unavailable.");
      }
   }

   public static class TerminationMiddlewareExtensions
   {
      public static IApplicationBuilder UseTerminationMiddleware(this IApplicationBuilder builder)
      {
         return builder.UseMiddleware<TerminationMiddleware>();
      }
   }
}