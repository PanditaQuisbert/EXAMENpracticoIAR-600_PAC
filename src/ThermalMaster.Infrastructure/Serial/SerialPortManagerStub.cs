using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Infrastructure.Serial;

/// <summary>
/// Administrador de puerto serial real basado en Win32 (CreateFile/ReadFile/WriteFile/SetCommState),
/// compatible con puertos COM virtuales (COMPIM/com0com) en Windows sin depender del paquete NuGet System.IO.Ports.
/// </summary>
public class SerialPortManager : ISerialPortManager, IDisposable
{
    private readonly object _sync = new();
    private IntPtr _handle = IntPtr.Zero;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private readonly StringBuilder _lineBuffer = new();

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public string CurrentPort { get; private set; } = string.Empty;
    public int CurrentBaudRate { get; private set; } = 9600;

    public event EventHandler<TelemetryRecord>? TelemetryReceived;
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? RawDataReceived;
    public event EventHandler<string>? RawDataSent;

    public Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            try
            {
                if (_handle != IntPtr.Zero && _handle != INVALID_HANDLE_VALUE)
                {
                    if (string.Equals(CurrentPort, portName, StringComparison.OrdinalIgnoreCase) && CurrentBaudRate == baudRate)
                        return Task.FromResult(true);
                    DisconnectInternal();
                }

                SetState(ConnectionState.Connecting, portName, baudRate);

                var path = NormalizePortPath(portName);
                var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
                if (handle == INVALID_HANDLE_VALUE)
                {
                    SetState(ConnectionState.Error, portName, baudRate);
                    SetState(ConnectionState.Disconnected, portName, baudRate);
                    return Task.FromResult(false);
                }

                if (!ConfigurePort(handle, baudRate))
                {
                    CloseHandle(handle);
                    SetState(ConnectionState.Error, portName, baudRate);
                    SetState(ConnectionState.Disconnected, portName, baudRate);
                    return Task.FromResult(false);
                }

                _handle = handle;
                CurrentPort = portName;
                CurrentBaudRate = baudRate;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _readTask = Task.Run(() => ReadLoop(_cts.Token));
                SetState(ConnectionState.Connected, portName, baudRate);
                return Task.FromResult(true);
            }
            catch
            {
                try { DisconnectInternal(); } catch { }
                SetState(ConnectionState.Error, portName, baudRate);
                SetState(ConnectionState.Disconnected, portName, baudRate);
                return Task.FromResult(false);
            }
        }
    }

    public async Task DisconnectAsync()
    {
        Task? readTask;
        lock (_sync)
        {
            readTask = _readTask;
            DisconnectInternal();
            SetState(ConnectionState.Disconnected, CurrentPort, CurrentBaudRate);
        }
        if (readTask != null)
        {
            try { await readTask.ConfigureAwait(false); } catch { }
        }
    }

    private void DisconnectInternal()
    {
        try { _cts?.Cancel(); } catch { }
        if (_handle != IntPtr.Zero && _handle != INVALID_HANDLE_VALUE)
        {
            try { CloseHandle(_handle); } catch { }
            _handle = IntPtr.Zero;
        }
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _readTask = null;
        _lineBuffer.Clear();
    }

    public Task<bool> SendCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            IntPtr handle;
            lock (_sync) handle = _handle;
            if (handle == IntPtr.Zero || handle == INVALID_HANDLE_VALUE)
                return Task.FromResult(false);

            var payload = BuildCommandPayload(command);
            var bytes = Encoding.ASCII.GetBytes(payload);
            if (!WriteFile(handle, bytes, (uint)bytes.Length, out var written, IntPtr.Zero) || written != (uint)bytes.Length)
                return Task.FromResult(false);

            RawDataSent?.Invoke(this, payload.TrimEnd('\r', '\n'));
            return Task.FromResult(true);
        }
        catch
        {
            SetState(ConnectionState.Error, CurrentPort, CurrentBaudRate);
            return Task.FromResult(false);
        }
    }

    public string[] GetAvailablePorts()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#pragma warning disable CA1416
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (key != null)
                {
                    return key.GetValueNames()
                        .Select(n => key.GetValue(n)?.ToString())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
#pragma warning restore CA1416
            }
        }
        catch { }
        return Array.Empty<string>();
    }

    private void ReadLoop(CancellationToken token)
    {
        var buffer = new byte[256];
        while (!token.IsCancellationRequested)
        {
            IntPtr handle;
            lock (_sync) handle = _handle;
            if (handle == IntPtr.Zero || handle == INVALID_HANDLE_VALUE) break;

            try
            {
                if (!ReadFile(handle, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err == 995 || token.IsCancellationRequested) break; // aborted
                    Thread.Sleep(50);
                    continue;
                }

                if (read == 0)
                {
                    Thread.Sleep(20);
                    continue;
                }

                var chunk = Encoding.ASCII.GetString(buffer, 0, (int)read);
                ProcessChunk(chunk);
            }
            catch
            {
                if (token.IsCancellationRequested) break;
                Thread.Sleep(50);
            }
        }
    }

    private void ProcessChunk(string chunk)
    {
        List<string> lines = new();
        lock (_sync)
        {
            _lineBuffer.Append(chunk);
            while (true)
            {
                var text = _lineBuffer.ToString();
                var idx = text.IndexOf('\n');
                if (idx < 0) break;
                var line = text.Substring(0, idx).Trim('\r').Trim();
                _lineBuffer.Remove(0, idx + 1);
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
            // evitar crecimiento indefinido ante basura sin fin de línea
            if (_lineBuffer.Length > 4096)
                _lineBuffer.Clear();
        }

        foreach (var line in lines)
        {
            RawDataReceived?.Invoke(this, line);
            TryParseTelemetry(line);
        }
    }

    private void TryParseTelemetry(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            // Solo emitir telemetría cuando parece trama de sensores
            if (root.TryGetProperty("temp", out _) || root.TryGetProperty("temperature", out _) || root.TryGetProperty("temperatureC", out _))
            {
                var record = ParseTelemetry(root);
                if (record != null)
                    TelemetryReceived?.Invoke(this, record);
            }
        }
        catch
        {
            // JSON inválido: se ignora; se expone por RawDataReceived para diagnóstico
        }
    }

    private static TelemetryRecord? ParseTelemetry(JsonElement root)
    {
        try
        {
            double temp = GetDouble(root, "temp", "temperature", "temperatureC");
            double hum = root.TryGetProperty("hum", out var h) ? ReadDoubleValue(h)
                : root.TryGetProperty("humidityPct", out var h2) ? ReadDoubleValue(h2)
                : root.TryGetProperty("humidity", out var h3) ? ReadDoubleValue(h3) : 0;

            if (double.IsNaN(temp) || double.IsInfinity(temp)) return null;
            if (double.IsNaN(hum) || double.IsInfinity(hum)) hum = 0;
            if (hum < 0) hum = 0; if (hum > 100) hum = 100;

            var fanOn = GetBoolInt(root, "fan", "fanStatus");
            var lampOn = GetBoolInt(root, "lamp", "lampStatus");
            var mode = ParseMode(GetString(root, "mode") ?? "AUTO");
            var alarm = root.TryGetProperty("alarm", out var a) && a.ValueKind == JsonValueKind.Number && a.TryGetInt32(out var av) ? av : 0;

            double min = 18.0, max = 28.0, alarmTh = 36.0;
            var severity = AlertSeverity.Normal;
            if (temp < min) severity = AlertSeverity.Warning;           // FR�O
            else if (temp <= max) severity = AlertSeverity.Normal;     // NORMAL
            else if (temp < alarmTh) severity = AlertSeverity.Warning; // CALOR
            else severity = AlertSeverity.Critical;                    // ALARMA

            return new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                TemperatureC = Math.Round(temp, 2),
                HumidityPct = Math.Round(hum, 2),
                FanStatus = fanOn ? FanState.On : FanState.Off,
                LampStatus = lampOn ? LampState.On : LampState.Off,
                Mode = mode,
                Severity = severity,
                MinThreshold = min,
                MaxThreshold = max,
                ProteusAcknowledged = true
            };
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCommandPayload(DeviceCommand command)
    {
        string type = (command.CommandType ?? string.Empty).Trim().ToUpperInvariant();
        string? p = command.Parameter;

        object obj = type switch
        {
            "FAN_ON" => new { cmd = "FAN_ON" },
            "FAN_OFF" => new { cmd = "FAN_OFF" },
            "LAMP_ON" => new { cmd = "LAMP_ON" },
            "LAMP_OFF" => new { cmd = "LAMP_OFF" },
            "SET_MODE" => new { cmd = "SET_MODE", val = NormalizeModeParam(p) },
            "SET_MODE_AUTO" => new { cmd = "SET_MODE", val = "AUTO" },
            "SET_MODE_MANUAL" => new { cmd = "SET_MODE", val = "MANUAL" },
            "SET_LIMITS" => BuildSetLimits(p),
            "PING" => new { cmd = "PING" },
            _ => new { cmd = type, val = p }
        };

        return JsonSerializer.Serialize(obj) + "\n";
    }

    private static object BuildSetLimits(string? parameter)
    {
        double min = 18.0, max = 28.0;
        if (!string.IsNullOrWhiteSpace(parameter))
        {
            var parts = parameter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 &&
                double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mi) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ma))
            { min = mi; max = ma; }
            else
            {
                var nums = Regex.Matches(parameter, @"-?\d+(?:[\.,]\d+)?");
                if (nums.Count >= 2)
                {
                    double.TryParse(nums[0].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out min);
                    double.TryParse(nums[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out max);
                }
            }
        }
        return new { cmd = "SET_LIMITS", min, max };
    }

    private static string NormalizeModeParam(string? p)
    {
        var v = (p ?? "AUTO").Trim().ToUpperInvariant();
        return v is "MANUAL" or "MANUAL_MODE" or "MANUALMODE" ? "MANUAL" : "AUTO";
    }

    private static OperatingMode ParseMode(string value)
    {
        var v = value.Trim().ToUpperInvariant();
        return v is "MANUAL" or "MANUAL_MODE" ? OperatingMode.Manual : OperatingMode.Automatic;
    }

    private static double GetDouble(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var el)) return ReadDoubleValue(el);
        }
        return double.NaN;
    }

    private static double ReadDoubleValue(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
        if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ds)) return ds;
        return double.NaN;
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var el))
        {
            if (el.ValueKind == JsonValueKind.String) return el.GetString();
            return el.ToString();
        }
        return null;
    }

    private static bool GetBoolInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el)) continue;
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i != 0;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim().ToUpperInvariant();
                if (s is "1" or "ON" or "TRUE") return true;
                if (s is "0" or "OFF" or "FALSE") return false;
            }
        }
        return false;
    }

    private static string NormalizePortPath(string portName)
    {
        if (portName.StartsWith(@"\\.\", StringComparison.Ordinal)) return portName;
        return @"\\.\" + portName;
    }

    private static bool ConfigurePort(IntPtr handle, int baudRate)
    {
        if (!GetCommState(handle, out var dcb)) return false;
        dcb.BaudRate = (uint)baudRate;
        dcb.ByteSize = 8;
        dcb.Parity = 0;   // NOPARITY
        dcb.StopBits = 0; // ONESTOPBIT
        dcb.Flags = 0;
        dcb.Flags |= 1;      // fBinary
        dcb.Flags |= (1 << 1); // fParity? no, keep 0
        dcb.Flags = 1; // binary
        if (!SetCommState(handle, ref dcb)) return false;

        var timeouts = new COMMTIMEOUTS
        {
            ReadIntervalTimeout = 50,
            ReadTotalTimeoutMultiplier = 0,
            ReadTotalTimeoutConstant = 100,
            WriteTotalTimeoutMultiplier = 0,
            WriteTotalTimeoutConstant = 1000
        };
        if (!SetCommTimeouts(handle, ref timeouts)) return false;
        SetupComm(handle, 4096, 4096);
        PurgeComm(handle, PURGE_RXCLEAR | PURGE_TXCLEAR);
        return true;
    }

    private void SetState(ConnectionState state, string? port, int baud)
    {
        State = state;
        if (!string.IsNullOrWhiteSpace(port)) CurrentPort = port;
        if (baud > 0) CurrentBaudRate = baud;
        try { ConnectionStateChanged?.Invoke(this, state); } catch { }
    }

    public void Dispose() => DisconnectAsync().GetAwaiter().GetResult();

    #region Win32
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint PURGE_RXCLEAR = 0x0008;
    private const uint PURGE_TXCLEAR = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct DCB
    {
        public uint DCBlength;
        public uint BaudRate;
        public uint Flags;
        public ushort wReserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EvtChar;
        public ushort wReserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COMMTIMEOUTS
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetCommState(IntPtr hFile, out DCB lpDCB);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCommState(IntPtr hFile, ref DCB lpDCB);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCommTimeouts(IntPtr hFile, ref COMMTIMEOUTS lpCommTimeouts);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetupComm(IntPtr hFile, uint dwInQueue, uint dwOutQueue);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PurgeComm(IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
    #endregion
}

/// <summary>
/// Alias conservado por compatibilidad con registros existentes.
/// </summary>
public class SerialPortManagerStub : SerialPortManager { }


