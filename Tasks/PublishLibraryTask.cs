using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace BuildScripts;

[TaskName("Publish Library")]
[IsDependentOn(typeof(PrepTask))]
public sealed class PublishLibraryTask : AsyncFrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.BuildSystem().IsRunningOnGitHubActions;

    public override async Task RunAsync(BuildContext context)
    {
        var knownRids = new string[] {
            "windows-x64",
            "linux-x64",
            "osx",
            "osx-x64",
            "osx-arm64",
            "ios",
            "android-arm64",
            "android-arm",
            "android-x86",
            "android-x64"
        };
        var availableRids = new List<string> ();

        foreach (var knownRid in knownRids) {
            if (context.DirectoryExists ($"{context.ArtifactsDir}/{knownRid}")) {
                availableRids.Add (knownRid);
            }
        }

        foreach (var r in availableRids)
            await context.BuildSystem().GitHubActions.Commands.UploadArtifact(DirectoryPath.FromString($"{context.ArtifactsDir}/{r}")), $"artifacts-{r}");

        if (availableRids.Any ())
            return;

        var rid = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            rid = "windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            rid = "osx";
        else
            rid = "linux";
        if (!(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && context.IsUniversalBinary))
        {
            rid += RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm or Architecture.Arm64 => "-arm64",
                _ => "-x64",
            };
        }
        await context.BuildSystem().GitHubActions.Commands.UploadArtifact(DirectoryPath.FromString(context.ArtifactsDir), $"artifacts-{rid}");
    }
}
