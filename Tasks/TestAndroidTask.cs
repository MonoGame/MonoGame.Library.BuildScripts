
namespace BuildScripts;

[TaskName("Test Android")]
public sealed class TestAndroidTask : FrostingTask<BuildContext>
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
        "linux-gate.so.1",
        "libOpenSLES.so",
        "liblog.so"
    };

    public override bool ShouldRun(BuildContext context) => context.IsRunningOnLinux();

    public override void Run(BuildContext context)
    {
        var ndk = System.Environment.GetEnvironmentVariable ("ANDROID_NDK_HOME");
        if (string.IsNullOrEmpty (ndk)) {
            context.Information($"SKIP: no ANDROID_NDK+HOME found.");
            return;
        }
        ///toolchains/llvm/prebuilt/*/bin/lld
        string readelf = string.Empty;
        var files = Directory.GetFiles (System.IO.Path.Combine(ndk, "toolchains/llvm/prebuilt"), "llvm-readelf", SearchOption.AllDirectories);
        if (files.Length > 0) {
            readelf = files.First (l => l.EndsWith ("llvm-readelf"));
        }
        if (string.IsNullOrEmpty (readelf)) {
            context.Information($"SKIP: could not find llvm-readelf");
            return;
        }

        foreach (var filePath in Directory.GetFiles(context.ArtifactsDir, "android-*", SearchOption.AllDirectories))
        {
            context.Information($"Checking: {filePath}");
            context.StartProcess(
                readelf,
                new ProcessSettings
                {
                    Arguments = $"--needed-libs {filePath}",
                    RedirectStandardOutput = true
                },
                out IEnumerable<string> processOutput);

            var passedTests = true;
            foreach (var line in processOutput)
            {
                if (line.Contains('[') || line.Contains(']'))
                    continue;
                var libPath = line.Trim();

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
