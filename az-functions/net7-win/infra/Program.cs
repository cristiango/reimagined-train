using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pulumi;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;

return await Pulumi.Deployment.RunAsync(() =>
{
    var tags = new Dictionary<string, string>
    {
        { "managed_by",  "pulumi"},
    };

    //Create an Azure Resource Group
    //supported Azure locations: https://azure.microsoft.com/en-us/global-infrastructure/locations/
    var resourceGroup = new ResourceGroup("resourceGroup", new ResourceGroupArgs
    {
        ResourceGroupName = "rg-ghainvoke",
        Location = "West Europe",
        Tags = tags
    });

    var storageAccount = new Pulumi.Azure.Storage.Account("st", new Pulumi.Azure.Storage.AccountArgs
    {
        ResourceGroupName = resourceGroup.Name,
        Location = resourceGroup.Location,
        AccountTier = "Standard",
        AccountReplicationType = "LRS"
    });

    var blobContainer = new BlobContainer("blobcontainer", new BlobContainerArgs
    {
        AccountName = storageAccount.Name,
        PublicAccess = PublicAccess.None,
        ResourceGroupName = resourceGroup.Name
    });

    var blob = new Blob("code-zip", new BlobArgs
    {
        AccountName = storageAccount.Name,
        ContainerName = blobContainer.Name,
        Type = BlobType.Block,
        ResourceGroupName = resourceGroup.Name,
        Source = new FileArchive(Path.Combine(GetRootDirectory(), "function.zip"))
    });

    var codeBlockUrl = SignedBlobReadUrl(blob, blobContainer, storageAccount.Name, resourceGroup);

    var appInsights = new Component("appInsights", new ComponentArgs
    {
        ApplicationType = ApplicationType.Web,
        Kind = "web",
        ResourceGroupName = resourceGroup.Name,
        Location = resourceGroup.Location,
        IngestionMode = IngestionMode.ApplicationInsights,
    });

    var appServicePlan = new Pulumi.Azure.AppService.ServicePlan("appServicePlan", new Pulumi.Azure.AppService.ServicePlanArgs
    {
        OsType = "Windows",
        ResourceGroupName = resourceGroup.Name,
        Location = resourceGroup.Location,
        SkuName = "Y1",
    });

    var app = new WindowsFunctionApp("app", new WindowsFunctionAppArgs
    {
        ResourceGroupName = resourceGroup.Name,
        Location = resourceGroup.Location,
        ServicePlanId = appServicePlan.Id,
        StorageAccountName = storageAccount.Name,
        StorageAccountAccessKey = storageAccount.PrimaryAccessKey,

        SiteConfig = new WindowsFunctionAppSiteConfigArgs
        {
            //AlwaysOn = true,
            Cors = new WindowsFunctionAppSiteConfigCorsArgs
            {
                AllowedOrigins = new[] { "https://portal.azure.com" }
            },
            ApplicationInsightsKey = appInsights.InstrumentationKey,
            Use32BitWorker = true,
            ApplicationStack = new WindowsFunctionAppSiteConfigApplicationStackArgs
            {
                DotnetVersion = "v7.0",
                UseDotnetIsolatedRuntime = true
            },
        },
        AppSettings = new InputMap<string>
            {
                { "FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated" },
                { "FUNCTIONS_EXTENSION_VERSION", "~4" },
                { "WEBSITE_RUN_FROM_PACKAGE", codeBlockUrl },
                { "APPINSIGHTS_INSTRUMENTATIONKEY", appInsights.InstrumentationKey },
                { "CODE_VERSION", blob.ContentMd5} //changing this value will trigger function restart
        },

    });

    return new Dictionary<string, object?>
    {
        ["endpoint"] = Output.Format($"https://{app.DefaultHostname}/api/version"),
        ["blobContentMd5"] = blob.ContentMd5
    };

});

static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, Output<string> accountName, ResourceGroup resourceGroup)
{
    var serviceSasToken = ListStorageAccountServiceSAS.Invoke(new ListStorageAccountServiceSASInvokeArgs
    {
        AccountName = accountName,
        Protocols = HttpProtocol.Https,
        SharedAccessStartTime = "2021-01-01",
        SharedAccessExpiryTime = "2030-01-01",
        Resource = SignedResource.C,
        ResourceGroupName = resourceGroup.Name,
        Permissions = Permissions.R,
        CanonicalizedResource = Output.Format($"/blob/{accountName}/{container.Name}"),
        ContentType = "application/json",
        CacheControl = "max-age=5",
        ContentDisposition = "inline",
        ContentEncoding = "deflate",
    }
        ).Apply(blobSAS => blobSAS.ServiceSasToken);

    return Output.Format($"https://{accountName}.blob.core.windows.net/{container.Name}/{blob.Name}?{serviceSasToken}");
}

static string GetRootDirectory()
{
    // There are two places where this is executed from
    //      1. At development time from the csproj directory.
    //      2. At deployment time from the infra directory in the deployment container.
    // We need to resolve where the lambda packages are.

    var rootDirectory = new DirectoryInfo(System.Environment.CurrentDirectory).Parent!;

    bool runningFromDeploymentContainer = Directory.Exists(Path.Combine(rootDirectory.FullName, "app"));
    if (!runningFromDeploymentContainer)
    {
        Log.Info("Running from csproj directory");
        var tempDir = rootDirectory.GetDirectories().SingleOrDefault(d => d.Name == "temp");
        if (tempDir != null)
        {
            rootDirectory = tempDir.GetDirectories("app").Single();
        }
    }
    Log.Info($"Root dir: {rootDirectory.FullName}");
    return rootDirectory.FullName;
}
