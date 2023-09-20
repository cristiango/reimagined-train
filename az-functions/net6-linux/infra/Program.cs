using System;
using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pulumi.AzureNative.DocumentDB;
using Pulumi.AzureNative.DocumentDB.Inputs;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using DocumentDB = Pulumi.AzureNative.DocumentDB;
using Authorization = Pulumi.AzureNative.Authorization;
using CapabilityArgs = Pulumi.AzureNative.DocumentDB.Inputs.CapabilityArgs;
using Web = Pulumi.AzureNative.Web;

return await Pulumi.Deployment.RunAsync(() =>
{
    var tags = new Dictionary<string, string>
    {
        { "managed_by", "pulumi" },
        { "owner", "cristiango" }
    };
    // Create an Azure Resource Group
    var resourceGroup = new ResourceGroup("resourceGroup", new ResourceGroupArgs
    {
        Tags = tags
    });

    var storageAccount = new StorageAccount("storage", new StorageAccountArgs
    {
        ResourceGroupName = resourceGroup.Name,
        Sku = new SkuArgs
        {
            Name = SkuName.Standard_LRS,
        },
        Kind = Pulumi.AzureNative.Storage.Kind.StorageV2
    });

    var appServicePlan = new AppServicePlan("function-linux-asp", new AppServicePlanArgs
    {
        ResourceGroupName = resourceGroup.Name,
        Kind = "Linux",
        Sku = new SkuDescriptionArgs
        {
            Tier = "Dynamic",
            Name = "Y1",
        },
        Reserved = true
    });

    var blobContainer = new BlobContainer("zips-container", new BlobContainerArgs
    {
        AccountName = storageAccount.Name,
        PublicAccess = PublicAccess.None,
        ResourceGroupName = resourceGroup.Name,
    });

    var blob = new Blob("zip", new BlobArgs
    {
        AccountName = storageAccount.Name,
        ContainerName = blobContainer.Name,
        ResourceGroupName = resourceGroup.Name,
        Type = BlobType.Block,
        Source = new FileArchive(Path.Combine(GetRootDirectory(), "artifacts", "function.zip"))
    });

    var codeBlockUrl = SignedBlobReadUrl(blob, blobContainer, storageAccount, resourceGroup);

    var appInsights = new Component("appInsights", new ComponentArgs
    {
        ApplicationType = ApplicationType.Web,
        Kind = "web",
        ResourceGroupName = resourceGroup.Name
    });
    
    //cosmos
    //cosmosDB
    var cosmosDBAccount =
        new Pulumi.AzureNative.DocumentDB.DatabaseAccount("database-account", new DatabaseAccountArgs()
        {
            ResourceGroupName = resourceGroup.Name,
            DatabaseAccountOfferType = DatabaseAccountOfferType.Standard,

            Locations =
            {
                new DocumentDB.Inputs.LocationArgs()
                {
                    LocationName = resourceGroup.Location,
                    FailoverPriority = 0,
                }
            },
            // ConsistencyPolicy = new ConsistencyPolicyArgs()
            // {
            //     DefaultConsistencyLevel = DefaultConsistencyLevel.Eventual
            // },
            Capabilities =
            {
                new CapabilityArgs()
                {
                    Name = "EnableServerless"
                }
            }
        });
    var db = new DocumentDB.SqlResourceSqlDatabase("sqldb", new SqlResourceSqlDatabaseArgs()
    {
        ResourceGroupName = resourceGroup.Name,
        AccountName = cosmosDBAccount.Name,
        Resource = new SqlDatabaseResourceArgs()
        {
            Id = "sqldb"
        }
    });

    var dbContainer = new DocumentDB.SqlResourceSqlContainer("container", new SqlResourceSqlContainerArgs()
    {
        ResourceGroupName = resourceGroup.Name,
        AccountName = cosmosDBAccount.Name,
        DatabaseName = db.Name,
        Resource = new SqlContainerResourceArgs()
        {
            Id = "container",
            PartitionKey = new ContainerPartitionKeyArgs()
            {
                Paths = { "/PK" },
                Kind = PartitionKind.Hash
            }
        }
    });

    var accountKeys = DocumentDB.ListDatabaseAccountKeys.Invoke(new ListDatabaseAccountKeysInvokeArgs()
    {
        AccountName = cosmosDBAccount.Name,
        ResourceGroupName = resourceGroup.Name
    });

    var apiId = Output.Create(Authorization.GetClientConfig.InvokeAsync())
        .Apply(clientConfig => Output.Format(
            $"/subscriptions/{clientConfig.SubscriptionId}/providers/Microsoft.Web/locations/{resourceGroup.Location}/managedApis/documentdb"));

    //API Connection to be used in a logic App
    var connection = new Web.Connection("cosmosDbConnection", new ConnectionArgs()
    {
        ResourceGroupName = resourceGroup.Name,
        Properties = new ApiConnectionDefinitionPropertiesArgs()
        {
            DisplayName = "cosmosdb_connection",
            Api = new ApiReferenceArgs()
            {
                Id = apiId,
            },
            ParameterValues =
            {
                { "databaseAccount", cosmosDBAccount.Name },
                { "accessKey", accountKeys.Apply(accountKeys => accountKeys.PrimaryMasterKey) }
            }
        }
    });

    var listCosmosDBConnectionStrings = DocumentDB.ListDatabaseAccountConnectionStrings.Invoke(
        new ListDatabaseAccountConnectionStringsInvokeArgs()
        {
            AccountName = cosmosDBAccount.Name,
            ResourceGroupName = resourceGroup.Name
        });

    var primaryCosmosDBConnectionString = listCosmosDBConnectionStrings.Apply(listResult =>
        listResult.ConnectionStrings
            .First(cs => string.Equals(cs.Description, "Primary SQL Connection String",
                StringComparison.InvariantCultureIgnoreCase))
            .ConnectionString);
    
    var app = new WebApp("app", new WebAppArgs
    {
        Kind = "functionApp",
        ResourceGroupName = resourceGroup.Name,
        ServerFarmId = appServicePlan.Id,
        SiteConfig = new SiteConfigArgs
        {
            AppSettings = new[]
            {
                new NameValuePairArgs
                {
                    Name = "AzureWebJobsStorage",
                    Value = GetStorageConnectionString(resourceGroup.Name, storageAccount.Name)
                },
                new NameValuePairArgs
                {
                    Name = "runtime",
                    Value = "dotnet",
                },
                new NameValuePairArgs
                {
                    Name = "FUNCTIONS_WORKER_RUNTIME",
                    Value = "dotnet"
                },
                new NameValuePairArgs
                {
                    Name = "FUNCTIONS_EXTENSION_VERSION",
                    Value = "~4"
                },
                new NameValuePairArgs
                {
                    Name = "WEBSITE_RUN_FROM_PACKAGE",
                    Value = codeBlockUrl,
                },
                new NameValuePairArgs
                {
                    Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                    Value = Output.Format($"InstrumentationKey={appInsights.InstrumentationKey}"),
                },
                new NameValuePairArgs
                {
                    Name = "CosmosDbConnectionString",
                    Value = primaryCosmosDBConnectionString
                },
                new NameValuePairArgs
                {
                    Name = "CosmosDbDatabaseName",
                    Value = db.Name
                },
                new NameValuePairArgs
                {
                    Name = "CosmosDbCollectionName",
                    Value = dbContainer.Name
                },
                new NameValuePairArgs
                {
                    Name = "COSMOS_ENDPOINT",
                    Value = cosmosDBAccount.DocumentEndpoint
                },
            }
        },
    });

    // Export the primary key of the Storage Account
    return new Dictionary<string, object?>
    {
        ["endpoint"] = Output.Format($"https://{app.DefaultHostName}/api/greetings"),
        ["cosmosDB PrimaryConnectionString"] = primaryCosmosDBConnectionString,
        ["cosmosDB dbConnection"] = connection.Id
    };
});

static Output<string> GetStorageConnectionString(Input<string> resourceGroupName, Input<string> accountName)
{
    // Retrieve the primary storage account key.
    var storageAccountKeys = ListStorageAccountKeys.Invoke(new ListStorageAccountKeysInvokeArgs
    {
        ResourceGroupName = resourceGroupName,
        AccountName = accountName
    });

    return storageAccountKeys.Apply(keys =>
    {
        var primaryStorageKey = keys.Keys[0].Value;

        // Build the connection string to the storage account.
        return Output.Format(
            $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={primaryStorageKey}");
    });
}

static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, StorageAccount account,
    ResourceGroup resourceGroup)
{
    var serviceSasToken = ListStorageAccountServiceSAS.Invoke(new ListStorageAccountServiceSASInvokeArgs
    {
        AccountName = account.Name,
        Protocols = HttpProtocol.Https,
        SharedAccessStartTime = "2021-01-01",
        SharedAccessExpiryTime = "2030-01-01",
        Resource = SignedResource.C,
        ResourceGroupName = resourceGroup.Name,
        Permissions = Permissions.R,
        CanonicalizedResource = Output.Format($"/blob/{account.Name}/{container.Name}"),
        ContentType = "application/json",
        CacheControl = "max-age=5",
        ContentDisposition = "inline",
        ContentEncoding = "deflate",
    }).Apply(blobSAS => blobSAS.ServiceSasToken);

    return Output.Format(
        $"https://{account.Name}.blob.core.windows.net/{container.Name}/{blob.Name}?{serviceSasToken}");
}

static string GetRootDirectory()
{
    // There are two places where this is executed from
    //      1. At development time from the csproj directory.
    //      2. At deployment time from the infra directory in the deployment container.
    // We need to resolve where the lambda packages are.
    var rootDirectory = new DirectoryInfo(Environment.CurrentDirectory).Parent!;
    // var tempDir = rootDirectory.GetDirectories().SingleOrDefault(d => d.Name == "artifacts");
    // if (tempDir != null)
    // {
    //     rootDirectory = tempDir.GetDirectories("app").Single();
    // }
    Log.Info($"Root dir: {rootDirectory.FullName}");
    return rootDirectory.FullName;
}