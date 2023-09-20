using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace GitHubWorkFlowDispatcher.Actions
{
    public class Version
    {
        [Function("version")]
        public async Task<IActionResult> Run(
                       [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req)
        {
            await Task.CompletedTask;
            var response = new Response
            {
                Description = "GitHub Actions Workflow Dispatcher",
                Version = Environment.GetEnvironmentVariable("CODE_VERSION") ?? "unknown"
            };
            return new OkObjectResult(response);
        }

        [Function("secure-version")]
        public async Task<IActionResult> RunSecure(
                       [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            await Task.CompletedTask;
            var response = new Response
            {
                Description = "Secure version",
                Version = Environment.GetEnvironmentVariable("CODE_VERSION") ?? "unknown"
            };
            return new OkObjectResult(response);
        }

        class Response
        {
            public string? Description { get; set; }
            public required string Version { get; set; }
            public DateTimeOffset Now => DateTimeOffset.Now;
        }
    }
}
