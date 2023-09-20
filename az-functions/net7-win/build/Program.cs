using System.IO.Compression;
using build;
using Ductus.FluentDocker;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string Clean = "clean";
const string Build = "build";
const string Test = "test";
const string PublishSrc = "publish-src";
const string PublishInfra = "publish-infra";
const string PublishDeploy = "publish-deploy";
const string BuildContainer = "build-container";
const string Default = "default";
const string PushContainer = "push-container";
const string Publish = "publish";

const string ArtifactsDirName = "artifacts";
const string DeploymentContainerImageName = "deployment-container";

var artifactsPath = Path.Combine(Environment.CurrentDirectory, ArtifactsDirName);
var tempPath = Path.Combine(Environment.CurrentDirectory, "temp");
var appPath = Path.Combine(tempPath, "app");
var deploymentImageTag = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "local";

Target(Clean, () =>
{
    Utils.CleanDirectory(artifactsPath);
    Utils.CleanDirectory(tempPath);

});

Target(Build, () =>
{
    Run("dotnet", "build functions.sln -c Release");
});

Target(Test, () =>
{
    Run("dotnet", $"test functions.sln -c Release");
});

Target(PublishSrc, DependsOn(Clean, Build), () =>
{
    Run("dotnet", $"publish src/functions.csproj -r win-x86 -c Release --no-self-contained -p:PublishReadyToRun=false --output {Path.Combine(tempPath, "function")}");
    Directory.CreateDirectory(appPath);
    ZipFile.CreateFromDirectory(Path.Combine(tempPath, "function"), Path.Combine(appPath, "function.zip"));
});

Target(PublishInfra, DependsOn(Clean), () =>
{
    Run("dotnet", $"publish infra/infra.csproj -r linux-musl-x64 -c Release --no-self-contained -p:PublishSingleFile=true --output {Path.Combine(appPath, "infra")}");
    File.Delete(Path.Combine(appPath, "infra", "Pulumi.yaml"));
    new FileInfo(Path.Combine(appPath, "infra", "PulumiPublish.yaml")).MoveTo(Path.Combine(appPath, "infra", "Pulumi.yaml"));
});

Target(PublishDeploy, DependsOn(Clean), () =>
{
    Run("dotnet", $"publish deploy/deploy.csproj -r linux-musl-x64 -c Release --no-self-contained -p:PublishSingleFile=true --output {appPath}");
});

Target(Publish, DependsOn(PublishSrc, PublishInfra, PublishDeploy));

Target(BuildContainer, DependsOn(Publish), () =>
{
    var pulumiAzureNativeVersion = Utils.GetPulumiPluginVersion("infra/infra.csproj", "AzureNative");
    var pulumiAzureVersion = Utils.GetPulumiPluginVersion("infra/infra.csproj", "Azure");
    Fd.DefineImage(DeploymentContainerImageName)
        .From("ghcr.io/cristiango/reimagined-train/pulumi-dotnet:latest")
        .Environment("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1")
        .Run($"pulumi plugin install resource azure-native {pulumiAzureNativeVersion}")
        .Run($"pulumi plugin install resource azure {pulumiAzureVersion}")
        .Copy("/app", "/app")
        .WorkingFolder(tempPath)
        .UseWorkDir("/app")
        .Build();

    Run("docker",
        $"tag {DeploymentContainerImageName}:latest ghcr.io/cristiango/reimagined-train/{DeploymentContainerImageName}:{deploymentImageTag}");
});

Target(PushContainer, () =>
{
    Run("docker", $"push ghcr.io/cristiango/reimagined-train/{DeploymentContainerImageName}:{deploymentImageTag}");
});

Target(Default, DependsOn(Test, BuildContainer));

await RunTargetsAndExitAsync(args);