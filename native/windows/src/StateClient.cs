using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace DshNotchPet
{
    /// <summary>
    /// Reads newline-delimited payloads from the host's named pipe. The
    /// reconnect, heartbeat-timeout and give-up policy mirrors the macOS Unix
    /// socket client one-for-one.
    /// </summary>
    internal sealed class StateClient : IDisposable
    {
        private readonly string rawPath;
        private readonly string pipeName;
        private readonly string stateFilePath;
        private readonly object gate = new object();
        private Thread thread;
        private Thread stateThread;
        private Timer watchdog;
        private NamedPipeClientStream pipe;
        private StreamReader reader;
        private volatile bool stopped;
        private volatile bool connected;
        private bool hasConnectedOnce;
        private bool fileActive;
        private DateTime disconnectedSince = DateTime.UtcNow;
        private DateTime lastMessage = DateTime.UtcNow;
        private DateTime lastFileMessage = DateTime.MinValue;
        private DateTime lastStateErrorLog = DateTime.MinValue;
        private readonly JavaScriptSerializer serializer;

        public event Action<PetPayload> PayloadReceived;
        public event Action<bool> ConnectionChanged;
        public event Action GaveUp;

        public StateClient(string pipePath, string stateFilePath)
        {
            rawPath = pipePath ?? string.Empty;
            pipeName = NormalizePipeName(rawPath);
            this.stateFilePath = stateFilePath ?? string.Empty;
            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 4 * 1024 * 1024;
        }

        public static string NormalizePipeName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string prefix = @"\\.\pipe\";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return path.Substring(prefix.Length);
            prefix = @"\\?\pipe\";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return path.Substring(prefix.Length);
            return path;
        }

        public void Start()
        {
            if (thread != null) return;
            AppLog.Write("pipe client starting " + rawPath);
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "DSHNotchPet.StateClient";
            thread.Start();
            if (!string.IsNullOrEmpty(stateFilePath))
            {
                stateThread = new Thread(RunStateFile);
                stateThread.IsBackground = true;
                stateThread.Name = "DSHNotchPet.StateFileClient";
                stateThread.Start();
            }
            watchdog = new Timer(WatchdogTick, null, 3000, 3000);
        }

        /// <summary>
        /// A named-pipe fallback. The host mirrors its snapshots to a
        /// per-process user-local file while no pipe client is attached.
        /// </summary>
        private void RunStateFile()
        {
            DateTime lastWrite = DateTime.MinValue;
            while (!stopped)
            {
                try
                {
                    if (!connected && File.Exists(stateFilePath))
                    {
                        DateTime write = File.GetLastWriteTimeUtc(stateFilePath);
                        if (write != lastWrite)
                        {
                            string json = File.ReadAllText(stateFilePath, Encoding.UTF8);
                            PetPayload payload = serializer.Deserialize<PetPayload>(json);
                            if (payload != null)
                            {
                                lastWrite = write;
                                lastFileMessage = DateTime.UtcNow;
                                hasConnectedOnce = true;
                                if (!fileActive) AppLog.Write("state mirror attached " + stateFilePath);
                                fileActive = true;
                                Action<bool> connection = ConnectionChanged;
                                if (connection != null) connection(true);
                                Action<PetPayload> handler = PayloadReceived;
                                if (handler != null) handler(payload);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DateTime now = DateTime.UtcNow;
                    if (!fileActive && (now - lastStateErrorLog).TotalSeconds > 30)
                    {
                        lastStateErrorLog = now;
                        AppLog.Write("state mirror read failed: " + ex.Message);
                    }
                }
                Sleep(150);
            }
        }

        private void Run()
        {
            while (!stopped)
            {
                if (!connected)
                {
                    if (!TryConnect())
                    {
                        if (hasConnectedOnce
                            && (DateTime.UtcNow - disconnectedSince).TotalSeconds > 20
                            && (DateTime.UtcNow - lastFileMessage).TotalSeconds > 20)
                        {
                            GiveUpNow();
                            return;
                        }
                        Sleep(1100);
                        continue;
                    }
                }

                try
                {
                    string line;
                    while (!stopped && (line = reader.ReadLine()) != null)
                    {
                        lastMessage = DateTime.UtcNow;
                        if (line.Length == 0) continue;
                        if (line.Length > 2 * 1024 * 1024)
                        {
                            AppLog.Write("pipe line too large, dropping");
                            continue;
                        }
                        try
                        {
                            PetPayload payload = serializer.Deserialize<PetPayload>(line);
                            if (payload != null)
                            {
                                Action<PetPayload> handler = PayloadReceived;
                                if (handler != null) handler(payload);
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLog.Write("payload decode failed: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!stopped) AppLog.Write("pipe read error: " + ex.Message);
                }

                Disconnect(true);
                if (!stopped) Sleep(1200);
            }
        }

        private bool TryConnect()
        {
            NamedPipeClientStream next = null;
            try
            {
                next = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.None);
                next.Connect(1200);
                lock (gate)
                {
                    if (stopped)
                    {
                        next.Dispose();
                        return false;
                    }
                    pipe = next;
                    reader = new StreamReader(next, new UTF8Encoding(false), false, 65536);
                    connected = true;
                    hasConnectedOnce = true;
                    fileActive = false;
                    disconnectedSince = DateTime.UtcNow;
                    lastMessage = DateTime.UtcNow;
                }
                AppLog.Write("pipe connected " + rawPath);
                Action<bool> handler = ConnectionChanged;
                if (handler != null) handler(true);
                return true;
            }
            catch (Exception ex)
            {
                if (next != null) next.Dispose();
                if (fileActive)
                {
                    // The state mirror is feeding the panel; do not spam the
                    // log while the named pipe stays unavailable.
                }
                else if (hasConnectedOnce)
                    AppLog.Write("pipe reconnect pending: " + ex.Message);
                else
                    AppLog.Write("waiting for host pipe: " + ex.Message);
                return false;
            }
        }

        private void Disconnect(bool notify)
        {
            bool wasConnected;
            lock (gate)
            {
                wasConnected = connected;
                connected = false;
                disconnectedSince = DateTime.UtcNow;
                try { if (reader != null) reader.Dispose(); } catch { }
                try { if (pipe != null) pipe.Dispose(); } catch { }
                reader = null;
                pipe = null;
            }
            if (wasConnected && notify && !stopped && !fileActive)
            {
                Action<bool> handler = ConnectionChanged;
                if (handler != null) handler(false);
            }
        }

        private void WatchdogTick(object state)
        {
            if (stopped) return;
            bool timedOut = false;
            bool gone = false;
            lock (gate)
            {
                if (connected && (DateTime.UtcNow - lastMessage).TotalSeconds > 16)
                {
                    timedOut = true;
                }
                else if (!connected && hasConnectedOnce
                    && (DateTime.UtcNow - disconnectedSince).TotalSeconds > 20
                    && (DateTime.UtcNow - lastFileMessage).TotalSeconds > 20)
                {
                    gone = true;
                }
            }
            if (timedOut)
            {
                AppLog.Write("pipe heartbeat timeout");
                Disconnect(true);
            }
            if (gone)
            {
                GiveUpNow();
            }
        }

        private void GiveUpNow()
        {
            if (stopped) return;
            stopped = true;
            AppLog.Write("pipe gone for 20s, exiting");
            Action handler = GaveUp;
            if (handler != null) handler();
        }

        private static void Sleep(int milliseconds)
        {
            int remaining = milliseconds;
            while (remaining > 0)
            {
                Thread.Sleep(Math.Min(remaining, 200));
                remaining -= 200;
            }
        }

        public void Stop()
        {
            stopped = true;
            Disconnect(false);
            stateThread = null;
            if (watchdog != null)
            {
                watchdog.Dispose();
                watchdog = null;
            }
            AppLog.Write("pipe client stopped");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
