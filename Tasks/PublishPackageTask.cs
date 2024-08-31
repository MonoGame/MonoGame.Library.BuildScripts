
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
                "osx",
            } 
            : new string[] {
                "windows-x64",
                "linux-x64",
                "osx-x64",
                "osx-arm64"
            };
        var optionalRids = new string[] {
                "ios",
                "android-arm64",
                "android-arm",
                "android-x86",
                "android-x64"
        };

        // Download built artifacts
        var downloadedRids = new List<string>();
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            foreach (var rid in requiredRids)
            {
                var directoryPath = $"runtimes/{rid}/native";
                if (context.DirectoryExists(directoryPath))
                    continue;

                context.CreateDirectory(directoryPath);
                await context.BuildSystem().GitHubActions.Commands.DownloadArtifact($"artifacts-{rid}", directoryPath);
                downloadedRids.Add (rid);
            }
            foreach (var rid in optionalRids)
            {
                var directoryPath = $"runtimes/{rid}/native";
                if (context.DirectoryExists(directoryPath))
                    continue;

                context.CreateDirectory(directoryPath);
                try {
                    await context.BuildSystem().GitHubActions.Commands.DownloadArtifact($"artifacts-{rid}", directoryPath);
                    downloadedRids.Add (rid);
                } catch {
                    // Rid not available, remove the directory.
                    context.DeleteDirectory (directoryPath,new DeleteDirectorySettings () { Recursive = true, Force = true});
                }
            }
        }

        var description = $"This package contains native libraries for {context.PackContext.LibraryName} built for usage with MonoGame.";
        
        var readMeName = "README.md";
        var readMePath = $"{readMeName}";

        var licensePath = context.PackContext.LicensePath;
        var licenseName = "LICENSE";

        if (licensePath.EndsWith(".txt")) licenseName += ".txt";
        else if (licensePath.EndsWith(".md")) licenseName += ".md";

        var librariesToInclude = from rid in downloadedRids from filePath in Directory.GetFiles($"runtimes/{rid}/native")
            select $"<Content Include=\"{filePath}\"><PackagePath>runtimes/{rid}/native</PackagePath></Content>";
        
        // Generate Project
        var projectData = await ReadEmbeddedResourceAsync("MonoGame.Library.X.txt");
        projectData = projectData.Replace("{X}", context.PackContext.LibraryName)
                                 .Replace("{Description}", description)
                                 .Replace("{LicensePath}", context.PackContext.LicensePath)
                                 .Replace("{ReadMePath}", readMeName)
                                 .Replace("{LicenseName}", licenseName)
                                 .Replace("{ReadMeName}", readMeName)
                                 .Replace("{LibrariesToInclude}", string.Join(Environment.NewLine, librariesToInclude));

        await File.WriteAllTextAsync($"MonoGame.Library.{context.PackContext.LibraryName}.csproj", projectData);
        await File.WriteAllTextAsync(readMePath, description);
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
