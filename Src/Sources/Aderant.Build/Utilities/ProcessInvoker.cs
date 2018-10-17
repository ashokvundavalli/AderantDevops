using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aderant.Build.Utilities {
    public class ProcessInvoker : IDisposable {

        private int? exitCode;
        private List<string> nonEmptyOutput = new List<string>();
        private System.Diagnostics.Process process;
        private SemaphoreSlim seenNullError;
        private SemaphoreSlim seenNullOutput;

        public ProcessInvoker(ProcessStartInfo startInfo) {
            StartInfo = startInfo;

            seenNullOutput = new SemaphoreSlim(1);
            seenNullError = new SemaphoreSlim(1);
        }

        public IReadOnlyCollection<string> ConsoleOutput {
            get {
                if (RecordConsoleOutput) {
                    return nonEmptyOutput.ToArray();
                }

                return Enumerable.Empty<string>().ToArray();
            }
        }

        public ProcessStartInfo StartInfo { get; }

        public string FileName => StartInfo.FileName;
        public string Arguments => StartInfo.Arguments;

        public Action<string> OnOutputLine { get; set; }
        public Action<string> OnErrorLine { get; set; }

        public bool RecordConsoleOutput { get; set; }

        public bool HasExited => process.HasExited;

        public int ProcessId => process.Id;

        public void Dispose() {
            seenNullOutput?.Dispose();
            seenNullError?.Dispose();
            process?.Dispose();

            process = null;
            seenNullError = null;
            seenNullOutput = null;
        }

        public virtual void Start() {
            seenNullOutput.Wait(0);
            seenNullError.Wait(0);

            StartInfo.UseShellExecute = false;
            StartInfo.ErrorDialog = false;
            StartInfo.CreateNoWindow = true;
            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;

            var p = new System.Diagnostics.Process {
                StartInfo = StartInfo
            };

            p.OutputDataReceived += OutputDataReceived;
            p.ErrorDataReceived += ErrorDataReceived;

            try {
                p.Start();
            } catch (Exception ex) {
                // Capture the error as stderr and exit code, then
                // clean up.
                exitCode = ex.HResult;
                OnErrorLine?.Invoke(ex.ToString());
                seenNullError.Release();
                seenNullOutput.Release();
                p.OutputDataReceived -= OutputDataReceived;
                p.ErrorDataReceived -= ErrorDataReceived;
                return;
            }

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.EnableRaisingEvents = true;

            // Close stdin so that if the process tries to read it will exit
            p.StandardInput.Close();

            process = p;
        }

        private void ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            try {
                if (e.Data == null) {
                    seenNullError.Release();
                } else {
                    RecordConsoleData(e.Data);
                    OnErrorLine?.Invoke(e.Data);
                }
            } catch (ObjectDisposedException) {
                ((System.Diagnostics.Process)sender).ErrorDataReceived -= ErrorDataReceived;
            }
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e) {
            try {
                if (e.Data == null) {
                    seenNullOutput.Release();
                } else {
                    RecordConsoleData(e.Data);
                    OnOutputLine?.Invoke(e.Data);
                }
            } catch (ObjectDisposedException) {
                ((System.Diagnostics.Process)sender).OutputDataReceived -= OutputDataReceived;
            }
        }

        private void RecordConsoleData(string e) {
            if (RecordConsoleOutput) {
                string text = e.Trim();
                if (text.Length > 0) {
                    nonEmptyOutput.Add(text);
                }
            }
        }

        public int Kill() {
            try {
                if (process != null && !process.HasExited) {
                    process.Kill();
                }
            } catch (SystemException) {
            }

            return process?.ExitCode ?? 1;
        }

        public int? Wait(int milliseconds) {
            if (exitCode != null) {
                return exitCode;
            }

            var cts = new CancellationTokenSource(milliseconds);
            try {
                var t = WaitAsync(cts.Token);
                try {
                    t.Wait(cts.Token);
                    return t.Result;
                } catch (AggregateException ae) when (ae.InnerException != null) {
                    throw ae.InnerException;
                }
            } catch (OperationCanceledException) {
                return null;
            }
        }

        public async Task<int> WaitAsync(CancellationToken cancellationToken) {
            if (exitCode != null) {
                return exitCode.Value;
            }

            if (process == null) {
                throw new InvalidOperationException("Process was not started");
            }

            if (!process.HasExited) {
                await seenNullOutput.WaitAsync(cancellationToken).ConfigureAwait(false);
                await seenNullError.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            for (var i = 0; i < 5 && !process.HasExited; i++) {
                await Task.Delay(100);
            }

            Debug.Assert(process.HasExited, "Process still has not exited.");
            return process.ExitCode;
        }
    }
}
