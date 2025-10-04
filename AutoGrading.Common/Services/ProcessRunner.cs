using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoGrading.Common.Services
{
    /// <summary>
    /// Manages running external processes (EXE or DLL via dotnet), capturing output line-by-line.
    /// Supports stdin for inputs. Outputs are buffered and can be read/cleared per step.
    /// Expansion: Add custom arguments, environment variables, or stderr handling.
    /// </summary>
    public class ProcessRunner : IDisposable
    {
        private Process? _process;
        private readonly StringBuilder _stdoutBuffer = new();
        private readonly object _bufLock = new();

        public event Action<string>? OnStdoutLine;

        public bool IsRunning => _process != null && !_process.HasExited;

        public async Task StartAsync(string executableOrDllPath, string args = "", string workingDirectory = "", CancellationToken ct = default)
        {
            ProcessStartInfo psi;
            if (executableOrDllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var dotnetPath = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                    ? "dotnet" : "/usr/bin/dotnet";  // Expansion: Configurable dotnet path
                psi = new ProcessStartInfo
                {
                    FileName = dotnetPath,
                    Arguments = $"\"{executableOrDllPath}\" {args}".Trim(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? System.IO.Path.GetDirectoryName(executableOrDllPath) ?? "" : workingDirectory
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = executableOrDllPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? System.IO.Path.GetDirectoryName(executableOrDllPath) ?? "" : workingDirectory
                };
            }

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (_bufLock) { _stdoutBuffer.AppendLine(e.Data); }
                    OnStdoutLine?.Invoke(e.Data + "\n");
                }
            };
            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (_bufLock) { _stdoutBuffer.AppendLine(e.Data); }
                    OnStdoutLine?.Invoke(e.Data + "\n");
                }
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await Task.Delay(10, ct).ConfigureAwait(false);  // Expansion: Configurable startup delay
        }

        public async Task WriteInputAsync(string text)
        {
            if (_process != null && _process.StartInfo.RedirectStandardInput && _process.StandardInput != null)
            {
                await _process.StandardInput.WriteLineAsync(text).ConfigureAwait(false);
                await _process.StandardInput.FlushAsync().ConfigureAwait(false);
            }
        }

        public string ReadAndClearBuffer()
        {
            lock (_bufLock)
            {
                var s = _stdoutBuffer.ToString();
                _stdoutBuffer.Clear();
                return s;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch { /* Ignore for cleanup */ }
        }

        public void Dispose()
        {
            _ = StopAsync();
            _process?.Dispose();
        }
    }
}