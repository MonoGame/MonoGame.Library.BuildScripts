
namespace BuildScripts;

[TaskName("Test macOS")]
public sealed class TestMacOSTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnMacOs();

    public override void Run(BuildContext context)
    {
        foreach (var filePath in Directory.GetFiles(context.ArtifactsDir, "*", SearchOption.AllDirectories))
        {
            context.Information($"Checking: {filePath}");
            context.StartProcess(
                "dyld_info",
                new ProcessSettings
                {
                    Arguments = $"-dependents {filePath}",
                    RedirectStandardOutput = true
                },
                out IEnumerable<string> processOutput);

            var processOutputList = processOutput.ToList();
            var passedTests = true;
            for (int i = 3; i < processOutputList.Count; i++)
            {
                var libPath = processOutputList[i].Trim().Split(' ')[^1];
                if (libPath.Contains('['))
                {
                    i += 2;
                    continue;
                }

                if (libPath.StartsWith("/usr/lib/") || libPath.StartsWith("/System/Library/Frameworks/"))
                {
                    context.Information($"VALID linkage: {libPath}");
                }
                else
                {
                    context.Information($"INVALID linkage: {libPath}");
                    passedTests = false;
                }
            }

            if (!passedTests)
            {
                throw new Exception("Invalid library linkage detected!");
            }

            // check if correct architectures are supported
            context.StartProcess(
                "file",
                new ProcessSettings
                {
                    Arguments = filePath,
                    RedirectStandardOutput = true
                },
                out processOutput);

            bool x86_64 = false;
            bool arm64 = false;

            processOutputList = processOutput.ToList();

            for (int i = 0; i < processOutputList.Count; i++)
            {
                var architecture = processOutputList[i];
                if (architecture.Contains("x86_64"))
                    x86_64 = true;
                else if (architecture.Contains("arm64"))
                    arm64 = true;
            }

            if (x86_64)
            {
                context.Information($"ARCHITECTURE: x86_64");
            }

            if (arm64)
            {
                context.Information($"ARCHITECTURE: arm64");
            }

            if (context.IsUniversalBinary && filePath.Contains ("osx") && !(arm64 && x86_64))
            {
                context.Information($"INVALID universal binary");
                throw new Exception("An universal binary hasn't been generated!");
            }

            context.Information("");
        }
    }
}
