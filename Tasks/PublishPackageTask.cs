
namespace BuildScripts;

[TaskName("Package")]
public sealed class PublishPackageTask : AsyncFrostingTask<BuildContext>
{
    private static async Task<string> ReadEmbeddedResourceAsync(string resourceName)
    {
        await using var stream = typeof(PublishPackageTask).Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task SaveEmbeddedResourceAsync(string resourceName, string outPath)
    {
        if (File.Exists(outPath))
            File.Delete(outPath);

        await using var stream = typeof(PublishPackageTask).Assembly.GetManifestResourceStream(resourceName)!;
        await using var writer = File.Create(outPath);
        await stream.CopyToAsync(writer);
        writer.Close();
    }

    public override async Task RunAsync(BuildContext context)
    {
        var requiredRids = context.IsUniversalBinary ?
            new string[]
            {
                "windows-x64",
                "linux-x64",
                "osx"
            } 
            : new string[] {
                "windows-x64",
                "linux-x64",
                "osx-x64",
                "osx-arm64"
            };

        // Download built artifacts
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            foreach (var rid in requiredRids)
            {
                var directoryPath = $"runtimes/{rid}/native";
                if (context.DirectoryExists(directoryPath))
                    continue;

                context.CreateDirectory(directoryPath);
                await context.BuildSystem().GitHubActions.Commands.DownloadArtifact($"artifacts-{rid}", directoryPath);
            }
        }

        // Generate Project
        var projectData = await ReadEmbeddedResourceAsync("MonoGame.Library.X.txt");
        projectData = projectData.Replace("{X}", context.PackContext.LibraryName);
        projectData = projectData.Replace("{LicencePath}", context.PackContext.LicensePath);

        if (context.PackContext.LicensePath.EndsWith(".txt"))
            projectData = projectData.Replace("{LicenceName}", "LICENSE.txt").Replace ("{LicencePackagePath}", "LICENSE.txt");
        else if (context.PackContext.LicensePath.EndsWith(".md"))
            projectData = projectData.Replace("{LicenceName}", "LICENSE.md").Replace ("{LicencePackagePath}", "LICENSE.md");
        else if (context.PackContext.LicensePath.EndsWith ("LICENSE"))
            // https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu5030#issue
            projectData = projectData.Replace("{LicenceName}", "LICENSE").Replace ("{LicencePackagePath}", "");
        else
            projectData = projectData.Replace("{LicenceName}", "LICENSE").Replace ("{LicencePackagePath}", "LICENSE");

        var librariesToInclude = from rid in requiredRids from filePath in Directory.GetFiles($"runtimes/{rid}/native")
            select $"<Content Include=\"{filePath}\"><PackagePath>runtimes/{rid}/native</PackagePath></Content>";
        projectData = projectData.Replace("{LibrariesToInclude}", string.Join(Environment.NewLine, librariesToInclude));

        await File.WriteAllTextAsync($"MonoGame.Library.{context.PackContext.LibraryName}.csproj", projectData);
        await SaveEmbeddedResourceAsync("Icon.png", "Icon.png");

        // Build
        var dnMsBuildSettings = new DotNetMSBuildSettings();
        dnMsBuildSettings.WithProperty("Version", context.PackContext.Version);
        dnMsBuildSettings.WithProperty("RepositoryUrl", context.PackContext.RepositoryUrl);
        
        context.DotNetPack($"MonoGame.Library.{context.PackContext.LibraryName}.csproj", new DotNetPackSettings
        {
            MSBuildSettings = dnMsBuildSettings,
            Verbosity = DotNetVerbosity.Minimal,
            Configuration = "Release"
        });

        // Upload Artifacts
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            foreach (var nugetPath in context.GetFiles("bin/Release/*.nupkg"))
            {
                await context.BuildSystem().GitHubActions.Commands.UploadArtifact(nugetPath, nugetPath.GetFilename().ToString());
                
                if (context.PackContext.IsTag)
                {
                    context.DotNetNuGetPush(nugetPath, new()
                    {
                        ApiKey = context.EnvironmentVariable("GITHUB_TOKEN"),
                        Source = $"https://nuget.pkg.github.com/{context.PackContext.RepositoryOwner}/index.json"
                    });
                }
            }
        }
    }
}
