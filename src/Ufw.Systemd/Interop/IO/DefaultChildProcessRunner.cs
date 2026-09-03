using System.ComponentModel;
using System.Diagnostics;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Interop.IO;

internal sealed class DefaultChildProcessRunner(IConfiguration configuration) : IChildProcessRunner
{
    public async Task<ChildProcessResult> RunAsync(ChildProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Command, nameof(request.Command));
        cancellationToken.ThrowIfCancellationRequested();

        using Process process = new()
        {
            StartInfo = CreateStartInfo(request)
        };

        if (configuration.Settings.DebugMode)
        {
            string args = string.Join(' ', request.Arguments);
            Console.WriteLine($"execute: '{request.Command} {args}'");
        }

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start child process '{request.Command}'.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            throw new ChildProcessException($"Failed to start child process '{request.Command}'.", ex);
        }

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        bool cancellationRequested = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationRequested = true;
            TryTerminate(process);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            TryTerminate(process);
            await ReapBestEffortAsync(process);
            throw new ChildProcessException($"Failed while waiting for child process '{request.Command}'.", ex);
        }

        try
        {
            string standardOutput = await standardOutputTask;
            string standardError = await standardErrorTask;

            if (configuration.Settings.DebugMode)
            {
                if (!string.IsNullOrEmpty(standardOutput))
                {
                    await Console.Out.WriteLineAsync(standardOutput);
                }
                if (!string.IsNullOrEmpty(standardError))
                {
                    await Console.Error.WriteLineAsync(standardError);
                }
            }

            return new ChildProcessResult(process.ExitCode, standardOutput, standardError, cancellationRequested);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            throw new ChildProcessException($"Failed to read child process output for '{request.Command}'.", ex);
        }
    }

    private static ProcessStartInfo CreateStartInfo(ChildProcessRequest request)
    {
        ProcessStartInfo startInfo = new(request.Command, request.Arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach ((string name, string value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and termination attempt.
        }
        catch (Exception ex) when (ex is Win32Exception or NotSupportedException or AggregateException)
        {
            // Keep ownership and wait for natural exit below if termination is unavailable or incomplete.
        }
    }

    private static async Task ReapBestEffortAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        catch (InvalidOperationException)
        {
            // No process remains to reap.
        }
    }
}
