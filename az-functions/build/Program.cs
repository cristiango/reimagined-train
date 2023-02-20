using System.IO.Compression;
using static Bullseye.Targets;
using static SimpleExec.Command;
using Target = Bullseye.Internal.Target;

const string ArtifactsDir = "artifacts";
const string Clean = "clean";
const string Build = "build";
const string PublishInfra = "publish-pack";

var artifactsPath = Path.Combine(Environment.CurrentDirectory, ArtifactsDir);
var tempPath = Path.Combine(Environment.CurrentDirectory, "temp");

Target(Clean, () =>
{
    Utils.CleanDirectory(artifactsPath);
    Utils.CleanDirectory(tempPath);
});

Target(Build, () =>
{
    Run("dotnet", "build functions.sln -c Release");
});

Target(PublishInfra, DependsOn(Clean, Build), () =>
{
    Run("dotnet", $"publish api/api.csproj -r linux -c Release --sc -o {tempPath}/app");
    ZipFile.CreateFromDirectory(Path.Combine(tempPath, "app"),Path.Combine(artifactsPath, "function.zip"));
});

Target("default", DependsOn(PublishInfra));

await RunTargetsAndExitAsync(args);