using System;
using System.IO;
using System.Threading;

namespace Aderant.Build.Tasks {
    internal class FileLock : IDisposable {
        private FileStream fileLock;
        private bool hasLock;

        private FileLock(FileStream fileLock) {
            this.fileLock = fileLock;
        }

        public static FileLock TryAcquire(string file, TimeSpan timeout) {
            if (timeout == TimeSpan.Zero) {
                throw new InvalidOperationException("You must specify a timeout");
            }

            FileStream stream = BuildFileLock(file);

            FileLock lockFile = new FileLock(stream);
            lockFile.Lock(timeout);
            
            return lockFile;
        }

        public bool HasLock {
            get { return hasLock; }
        }

        private void Lock(TimeSpan timeout) {
            bool tryAquire = true;

            var timer = new Timer(state => {
                Console.WriteLine("Elapsed.");
                tryAquire = false;
                ((Timer)state).Dispose();
            });

            timer.Change(timeout, TimeSpan.Zero);

            try {
                do {
                    try {
                        fileLock.Lock(0, Int32.MaxValue);
                        hasLock = true;

                        DisposeTimer(timer);

                        return;
                    } catch (IOException ex) {
                        Console.WriteLine(ex.Message);
                        if (ex.HResult == -2147024863) {
                            // Another process has this lock
                        } else {
                            throw;
                        }
                    }

                    Thread.Sleep((int) TimeSpan.FromSeconds(5).TotalMilliseconds);
                } while (tryAquire);
            } finally {
                DisposeTimer(timer);
            }
        }

        private static void DisposeTimer(Timer timer) {
            if (timer != null) {
                try {
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                    timer.Dispose();
                } catch {
                }
            }
        }

        private static FileStream BuildFileLock(string lockFile) {
            return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.Write | FileAccess.Read, FileShare.ReadWrite);
        }

        public void Dispose() {
            if (fileLock != null) {
                fileLock.Dispose();
                fileLock = null;
            }
            hasLock = false;
        }
    }
}