using System.Diagnostics;

namespace cdc_api.Controllers;

public static class Spawn
{
    public static string SpawnAndWait(string pathToExecutable, string args, string workingDir, ILogger debugWriter)
    {

        var shellConfig = new ProcessStartInfo();
        shellConfig.FileName = pathToExecutable;
        shellConfig.WorkingDirectory = workingDir;
        shellConfig.Arguments = args;
        shellConfig.RedirectStandardOutput = false;
        shellConfig.RedirectStandardError = false;
        shellConfig.UseShellExecute = false;
        shellConfig.CreateNoWindow = true;

        var shellProcess = new Process();
        shellProcess.StartInfo = shellConfig;
        shellProcess.EnableRaisingEvents = true;
        try
        {
            debugWriter.LogDebug($"\r\nshell: {pathToExecutable}\r\nargs:{args}");
            if (!shellProcess.Start())
            {
                debugWriter.LogDebug($"shell exited with code {shellProcess.ExitCode}:{shellProcess.StandardError.ReadToEnd()}");
                return default(string);
            }

            shellProcess.WaitForExit();
            debugWriter.LogDebug("end shell\r\n");

            if (shellProcess.ExitCode > 0)
                throw new Exception($"{pathToExecutable} {args} exited with code {shellProcess.ExitCode}");

            return "";
        }
        catch (Exception e)
        {
            debugWriter.LogError(e, "fail");
            throw;
        }
    }
}
