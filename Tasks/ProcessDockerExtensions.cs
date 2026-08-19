using System;
using System.Runtime.InteropServices;

namespace BuildScripts;

/// <summary>
/// Cake Context Extensions to provide support for building libraries or native
/// tools in a custom monogame docker image based on steams sniper image
/// for maximum linux compatability
/// </summary>
public static class ProcessDockerExtensions
{
    const string DOCKERIMAGE_X64 = "ghcr.io/monogame/steamrt-sniper-premake:v5.0.0-beta8-amd64";
    const string DOCKERIMAGE_ARM64 = "ghcr.io/monogame/steamrt-sniper-premake:v5.0.0-beta8-arm64";
    private static bool UseDocker(BuildContext context)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Environment.GetEnvironmentVariable ("CI") == "true")
        {
            var args = new ProcessArgumentBuilder();
            args.Append("docker");
            var settings = new ProcessSettings { Arguments = args };
            if (context.StartProcess("which", settings) == 0)
            {
                return true;
            }
        }
        return false;
    }

    public static int StartProcessWithDocker(this BuildContext context,  string command, ProcessSettings processSettings, string volumeMount = "")
    {
        return StartProcessWithDocker(context, command, processSettings.WorkingDirectory.FullPath, processSettings.Arguments, processSettings.EnvironmentVariables, volumeMount);
    }

    public static int StartProcessWithDocker(this BuildContext context,  string command, string workingDirectory, ProcessArgumentBuilder args, IDictionary<string, string> envVariables, string volumeMount = "")
    {
        var useDocker = UseDocker(context);
        if (useDocker)
        {
            var workdir = "/src";
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                workdir += $"/{workingDirectory}";
            }
            args.Prepend(command);
            args.Prepend(RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? DOCKERIMAGE_ARM64 : DOCKERIMAGE_X64);
            if (envVariables is not null)
            {
                foreach (var environmentVariable in envVariables)
                {
                    args.PrependSwitchQuoted(
                        "--env",
                        " ",
                        $"{environmentVariable.Key}={environmentVariable.Value}");
                }
            }
            args.PrependQuoted(workdir);
            args.Prepend("-w");
            args.PrependQuoted($"{System.IO.Path.GetFullPath(".")}:/src");
            args.Prepend("-v");
            args.Prepend("run");
            command = "docker";
        }
        var settings = new ProcessSettings {
            Arguments = args,
            WorkingDirectory = workingDirectory,
            NoWorkingDirectory = useDocker,
            EnvironmentVariables = useDocker ? null : envVariables,
        };
        return context.StartProcess(command, settings);
    }

    public static DirectoryPath MakeAbsoluteForDocker(this BuildContext context, DirectoryPath directoryPath)
    {
        var useDocker = UseDocker(context);
        if (useDocker)
        {
            var path = System.IO.Path.GetFullPath(directoryPath.FullPath);
            var relativePath = System.IO.Path.GetRelativePath(System.IO.Path.GetFullPath("."), path);
            return new DirectoryPath($"/src/{relativePath}");
        }
        return context.MakeAbsolute(directoryPath);
    }
}
