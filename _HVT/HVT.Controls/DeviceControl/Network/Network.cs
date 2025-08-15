using MathNet.Numerics.Statistics;
using NetMQ;
using NetMQ.Sockets;
using PropertyChanged;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace HVT.Controls
{
    [AddINotifyPropertyChangedInterface]
    public class Network : IDisposable
    {
        private PairSocket socket;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private NetMQPoller poller;
        private readonly object socketLock = new object();
        private bool disposed = false;
        private System.Timers.Timer heartbeatTimer;
        private System.Timers.Timer connectionTimeoutTimer;
        private DateTime lastHeartbeatReceived = DateTime.Now;
        private const int HEARTBEAT_INTERVAL = 5000; // 5 seconds
        private const int CONNECTION_TIMEOUT = 15000; // 15 seconds
        private const int RECONNECT_DELAY = 3000; // 3 seconds
        private const int MAX_RECONNECT_ATTEMPTS = 5;
        private int reconnectAttempts = 0;

        [JsonIgnore]
        public string Role { get => IsMain ? "MAIN" : "SUB"; }
        public string IP { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "8000";
        public bool IsMain { get; set; } = true;
        public bool Use { get; set; } = false;
        public MachineStatus StatusMain { get; set; } = MachineStatus.UNKOWN;
        public MachineStatus StatusSub { get; set; } = MachineStatus.UNKOWN;
        public bool IsNetworkConnected { get; set; } = false;

        [JsonIgnore]
        public SolidColorBrush StatusColor { get => IsNetworkConnected ? new SolidColorBrush(Color.FromRgb(5, 247, 5)) : new SolidColorBrush(Color.FromRgb(198, 198, 198)); }

        private void OnIsNetworkConnectedChanged()
        {
            IsNetworkDisconnected = !IsNetworkConnected;
        }

        public event EventHandler OnMainStartRequest;
        public event EventHandler OnMainCancelRequest;
        public event EventHandler<string> OnConnectionStateChanged;

        public bool IsNetworkDisconnected { get; set; } = true;

        private void OnMessageReceived(object sender, NetMQSocketEventArgs e)
        {
            if (disposed || cancellationTokenSource.IsCancellationRequested)
                return;

            try
            {
                string msg = null;
                lock (socketLock)
                {
                    if (socket != null && !socket.IsDisposed)
                    {
                        msg = socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string receivedMessage) 
                            ? receivedMessage : null;
                    }
                }

                if (string.IsNullOrEmpty(msg))
                    return;

                var result = JsonSerializer.Deserialize<Message>(msg);

                // Handle heartbeat messages
                if (result.MachineStatus == MachineStatus.HEARTBEAT)
                {
                    lastHeartbeatReceived = DateTime.Now;
                    return;
                }

                Utility.Debug.Write($"{result.SourceMachine} Status: {result.MachineStatus}", HVT.Utility.Debug.ContentType.Notify);

                if (result.SourceMachine == SourceMachine.MAIN)
                {
                    StatusMain = result.MachineStatus;

                    if (StatusMain == MachineStatus.CLDOWN)
                    {
                        OnMainStartRequest?.Invoke(sender, e);
                    }
                    if (StatusMain == MachineStatus.CLUP)
                    {
                        OnMainCancelRequest?.Invoke(sender, e);
                    }
                }
                if (result.SourceMachine == SourceMachine.SUB)
                {
                    StatusSub = result.MachineStatus;
                }
            }
            catch (ObjectDisposedException)
            {
                // Socket was disposed, ignore
            }
            catch (Exception ex)
            {
                Utility.Debug.Write($"⚠ Message processing error: {ex.Message}", Utility.Debug.ContentType.Error);
            }
        }

        public async Task<bool> StartAsync()
        {
            if (disposed)
                return false;

            try
            {
                Stop(); // Ensure clean state
                
                lock (socketLock)
                {
                    socket = new PairSocket();      
                    
                    // Configure socket options for stability (compatible with NetMQ 4.0.1.16)
                    socket.Options.Linger = TimeSpan.FromSeconds(1);

                    if (IsMain)
                    {
                        socket.Bind($"tcp://{IP}:{Port}");
                        Utility.Debug.Write($"🔌 Main machine bound to port {Port}...", Utility.Debug.ContentType.Notify);
                    }
                    else
                    {
                        socket.Connect($"tcp://{IP}:{Port}");
                        Utility.Debug.Write($"🔌 Sub machine connected to {IP}:{Port}...", Utility.Debug.ContentType.Notify);
                    }

                    socket.ReceiveReady += OnMessageReceived;

                    poller = new NetMQPoller { socket };
                    poller.RunAsync();
                }

                // Start heartbeat and connection monitoring
                InitializeConnectionMonitoring();

                IsNetworkConnected = true;
                reconnectAttempts = 0;
                OnConnectionStateChanged?.Invoke(this, "Connected");

                return true;
            }
            catch (Exception ex)
            {
                Utility.Debug.Write($"⚠ Network start failed: {ex.Message}", Utility.Debug.ContentType.Error);
                IsNetworkConnected = false;
                
                // Schedule reconnection attempt
                if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
                {
                    _ = Task.Delay(RECONNECT_DELAY).ContinueWith(async _ => await AttemptReconnection());
                }
                
                return false;
            }
        }

        public void Start()
        {
            _ = StartAsync();
        }

        public void Stop()
        {
            try
            {
                cancellationTokenSource?.Cancel();

                // Stop timers
                heartbeatTimer?.Stop();
                heartbeatTimer?.Dispose();
                heartbeatTimer = null;

                connectionTimeoutTimer?.Stop();
                connectionTimeoutTimer?.Dispose();
                connectionTimeoutTimer = null;

                lock (socketLock)
                {
                    if (poller != null)
                    {
                        if (poller.IsRunning)
                            poller.Stop();
                        poller.Dispose();
                        poller = null;
                    }

                    if (socket != null)
                    {
                        socket.ReceiveReady -= OnMessageReceived;
                        if (!socket.IsDisposed)
                            socket.Dispose();
                        socket = null;
                    }
                }

                IsNetworkConnected = false;
                OnConnectionStateChanged?.Invoke(this, "Disconnected");
            }
            catch (Exception ex)
            {
                Utility.Debug.Write($"⚠ Network stop error: {ex.Message}", Utility.Debug.ContentType.Error);
            }
        }

        public bool NotifyStatus(MachineStatus status)
        {
            if (!Use || !IsNetworkConnected || disposed)
                return false;

            try
            {
                var result = new Message 
                {
                    SourceMachine = IsMain ? SourceMachine.MAIN : SourceMachine.SUB,
                    MachineStatus = status,
                    Timestamp = DateTime.Now,
                };

                return SendMessageWithRetry(result, 3);
            }
            catch (Exception ex)
            {
                Utility.Debug.Write($"⚠ NotifyStatus error: {ex.Message}", Utility.Debug.ContentType.Error);
                return false;
            }
        }

        private bool SendMessageWithRetry(Message message, int maxRetries)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    lock (socketLock)
                    {
                        if (socket == null || socket.IsDisposed)
                            return false;

                        string json = JsonSerializer.Serialize(message);
                        
                        if (socket.TrySendFrame(TimeSpan.FromSeconds(2), json))
                        {
                            Utility.Debug.Write($"📤 Sent Status: {message.MachineStatus}", Utility.Debug.ContentType.Notify);

                            // Update local status
                            if (IsMain)
                                StatusMain = message.MachineStatus;
                            else
                                StatusSub = message.MachineStatus;

                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utility.Debug.Write($"⚠ Send attempt {attempt + 1} failed: {ex.Message}", Utility.Debug.ContentType.Error);
                }

                if (attempt < maxRetries - 1)
                    Thread.Sleep(100); // Brief delay between retries
            }

            // All retries failed, consider connection lost
            _ = HandleConnectionLoss();
            return false;
        }

        private void InitializeConnectionMonitoring()
        {
            // Heartbeat timer - send periodic heartbeat messages
            heartbeatTimer = new System.Timers.Timer(HEARTBEAT_INTERVAL);
            heartbeatTimer.Elapsed += (s, e) => SendHeartbeat();
            heartbeatTimer.Start();

            // Connection timeout timer - check if we're receiving heartbeats
            connectionTimeoutTimer = new System.Timers.Timer(CONNECTION_TIMEOUT);
            connectionTimeoutTimer.Elapsed += (s, e) => CheckConnectionTimeout();
            connectionTimeoutTimer.Start();

            lastHeartbeatReceived = DateTime.Now;
        }

        private void SendHeartbeat()
        {
            if (!IsNetworkConnected || disposed)
                return;

            var heartbeat = new Message
            {
                SourceMachine = IsMain ? SourceMachine.MAIN : SourceMachine.SUB,
                MachineStatus = MachineStatus.HEARTBEAT,
                Timestamp = DateTime.Now
            };

            SendMessageWithRetry(heartbeat, 1); // Only one attempt for heartbeat
        }

        private void CheckConnectionTimeout()
        {
            if (!IsNetworkConnected || disposed)
                return;

            if (DateTime.Now.Subtract(lastHeartbeatReceived).TotalMilliseconds > CONNECTION_TIMEOUT)
            {
                Utility.Debug.Write("⚠ Connection timeout detected", Utility.Debug.ContentType.Error);
                _ = HandleConnectionLoss();
            }
        }

        private async Task HandleConnectionLoss()
        {
            if (disposed)
                return;

            IsNetworkConnected = false;
            OnConnectionStateChanged?.Invoke(this, "Connection Lost");

            await AttemptReconnection();
        }

        private async Task AttemptReconnection()
        {
            if (disposed || reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
                return;

            reconnectAttempts++;
            Utility.Debug.Write($"🔄 Attempting reconnection {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}...", 
                               Utility.Debug.ContentType.Notify);

            await Task.Delay(RECONNECT_DELAY);

            if (await StartAsync())
            {
                Utility.Debug.Write("✅ Reconnection successful", Utility.Debug.ContentType.Notify);
            }
            else if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
            {
                // Schedule next attempt
                _ = Task.Delay(RECONNECT_DELAY).ContinueWith(async _ => await AttemptReconnection());
            }
            else
            {
                Utility.Debug.Write("❌ Max reconnection attempts reached", Utility.Debug.ContentType.Error);
                OnConnectionStateChanged?.Invoke(this, "Connection Failed");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
            cancellationTokenSource?.Dispose();
        }
    }
}
