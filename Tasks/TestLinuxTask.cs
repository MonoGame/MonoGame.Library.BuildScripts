
namespace BuildScripts;

[TaskName("Test Linux")]
public sealed class TestLinuxTask : FrostingTask<BuildContext>
{
    private static readonly string[] ValidLibs = {
        "linux-vdso.so",
        "libstdc++.so",
        "libgcc_s.so",
        "libc.so",
        "libm.so",
        "libdl.so",
        "libpthread.so",
        "/lib/ld-linux-",
        "/lib64/ld-linux-",
    };

    public override bool ShouldRun(BuildContext context) => context.IsRunningOnLinux();

    public override void Run(BuildContext context)
    {
        var rootFiles = Directory.GetFiles(context.ArtifactsDir);
        var linuxFiles = Directory.GetFiles(context.ArtifactsDir, "linux-*", SearchOption.AllDirectories);
        foreach (var filePath in rootFiles.Union (linuxFiles))
        {
            context.Information($"Checking: {filePath}");
            context.StartProcess(
                "ldd",
                new ProcessSettings
                {
                    Arguments = $"{filePath}",
                    RedirectStandardOutput = true
                },
                out IEnumerable<string> processOutput);

            var passedTests = true;
            foreach (var line in processOutput)
            {
                var libPath = line.Trim().Split(' ')[0];

                var isValidLib = false;
                foreach (var validLib in ValidLibs)
                {
                    if (libPath.StartsWith(validLib))
                    {
                        isValidLib = true;
                        break;
                    }
                }

                if (isValidLib)
                {
                    context.Information($"VALID: {libPath}");
                }
                else
                {
                    context.Information($"INVALID: {libPath}");
                    passedTests = false;
                }
            }

            if (!passedTests)
            {
                throw new Exception("Invalid library linkage detected!");
            }

            context.Information("");
        }
    }
}
