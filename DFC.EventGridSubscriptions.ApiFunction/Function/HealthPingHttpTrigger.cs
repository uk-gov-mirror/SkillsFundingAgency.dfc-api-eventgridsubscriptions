using DFC.Swagger.Standard.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace DFC.EventGridSubscriptions.ApiFunction.Function
{
    public class HealthPingHttpTrigger
    {
        private readonly ILogger<HealthPingHttpTrigger> logger;

        public HealthPingHttpTrigger(ILogger<HealthPingHttpTrigger> logger)
        {
            this.logger = logger;
        }

        [Function("HealthPing")]
        [Display(Name = "Health ping", Description = "Simple OK response to a health ping")]
        [Response(HttpStatusCode = (int)HttpStatusCode.OK, Description = "OK", ShowSchema = false)]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ping")] HttpRequest req)
        {
            logger.LogInformation("Health ping request received. Responding with 200 OK. Method: {Method}, Path: {Path}", req.Method, req.Path);

            return new OkResult();
        }
    }
}
