using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Newtonsoft.Json;

namespace api
{
    public class Greetings
    {
        private readonly IConfiguration _configuration;

        public Greetings(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("Greetings")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This updated from Pieter HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hi, {name}";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("secure")]
        public async Task<IActionResult> RunSecure(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            await Task.CompletedTask;
            log.LogInformation("C# HTTP trigger function processed a request.");

            var name = req.HttpContext.User.Identity.Name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("read")]
        public async Task<IActionResult> ReadFromDb(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
            HttpRequest req, ILogger log)
        {
            string id = req.Query["id"];

            //dynamoDB item connection
            //CosmosClient
            if (string.IsNullOrEmpty(id))
            {
                return new NotFoundResult();
            }

            var cosmosConnection = _configuration.Get<CosmosDbClientSettings>();

            var client = new CosmosClient(connectionString: cosmosConnection.CosmosDbConnectionString);
            var container = client.GetContainer(cosmosConnection.CosmosDbDatabaseName,
                cosmosConnection.CosmosDbCollectionName);

            ItemResponse<Product> item =
                await container.ReadItemAsync<Product>(id: id, partitionKey: new PartitionKey("gear-surf-surfboards"));

            return new OkObjectResult(item);
        }

        [FunctionName("save")]
        public async Task<IActionResult> SaveToDb(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = null)]
            HttpRequest req, ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Product data = JsonConvert.DeserializeObject<Product>(requestBody);


            var cosmosConnection = _configuration.Get<CosmosDbClientSettings>();

            CosmosClient client = new CosmosClient(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT", EnvironmentVariableTarget.Process),
                new DefaultAzureCredential()
            );
            var container = client.GetContainer(cosmosConnection.CosmosDbDatabaseName,
                cosmosConnection.CosmosDbCollectionName);

            ItemResponse<Product> createdItem = await container.CreateItemAsync<Product>(
                item: data,
                partitionKey: new PartitionKey("gear-surf-surfboards")
            );


            return new OkObjectResult(createdItem.Resource);
        }
        
        [FunctionName("readdatabases")]
        public static async Task<IActionResult> ReadDatabases(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            ILogger log)
        {
            log.LogTrace("Start function");

            CosmosClient client = new CosmosClient(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT", EnvironmentVariableTarget.Process),
                new DefaultAzureCredential()
            );

            using FeedIterator<DatabaseProperties> iterator = client.GetDatabaseQueryIterator<DatabaseProperties>();

            List<(string name, string uri)> databases = new();
            while(iterator.HasMoreResults)
            {
                foreach(DatabaseProperties database in await iterator.ReadNextAsync())
                {
                    log.LogTrace($"[Database Found]\t{database.Id}");
                    databases.Add((database.Id, database.SelfLink));
                }
            }

            return new OkObjectResult(databases);
        }
    }
}

public record Product(
    string id,
    string PK,
    string category,
    string name,
    int quantity,
    bool sale
);

public class CosmosDbClientSettings
{
    public string CosmosDbDatabaseName { get; set; }
    public string CosmosDbCollectionName { get; set; }
    public string CosmosDbConnectionString { get; set; }
}