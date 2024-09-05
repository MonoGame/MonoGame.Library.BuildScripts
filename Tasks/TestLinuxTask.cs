
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
        // android
        "linux-gate.so.1",
        "libOpenSLES.so",
        "liblog.so"
    };

    public override bool ShouldRun(BuildContext context) => context.IsRunningOnLinux();

    public override void Run(BuildContext context)
    {
        foreach (var filePath in Directory.GetFiles(context.ArtifactsDir, "*", SearchOption.AllDirectories))
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
                    if (!libPath.Contains ("android-arm")) {
                        context.Information($"INVALID: {libPath}");
                        passedTests = false;
                    }
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
