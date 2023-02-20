if ($args[0] -eq "local") {
    Write-Host "Building on local system..."
    dotnet run --project build -- $args[1..($args.Count)]
    exit 0;
}

Write-Host "Building in docker (use './build.ps1 local' to build without using docker)..."

$GitHubToken=$Env:AxiomGitHubToken

if ($GitHubToken -eq $null -or $GitHubToken -eq "") {
    Write-Error "GitHubToken environment variable empty or missing."
}

$tag="az-functions"

# Build the build environment image.
docker build `
--build-arg GitHubToken=$GitHubToken `
-f build.dockerfile `
--tag $tag.

#build inside docker
docker run --rm --name $tag `
-v /var/run/docker.sock:/var/run/docker.sock `
-v $PWD/artifacts:/repo/artifacts `
-v $PWD/temp:/repo/temp `
-v $PWD/.git:/repo/.git `
--network host `
-e NUGET_PACKAGES=/repo/temp/nuget-packages `
$tag `
dotnet run --project build/build.csproj -c Release -- $args