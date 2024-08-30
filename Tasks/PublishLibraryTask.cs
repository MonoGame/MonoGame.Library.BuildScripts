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

        foreach (var rid in availableRids)
            await context.BuildSystem().GitHubActions.Commands.UploadArtifact(DirectoryPath.FromString(context.ArtifactsDir), $"artifacts-{rid}");
    }
}
