using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Serilog;

namespace ThermalCyclerSimulator;

public class ThermalCyclerSimulator
{
    private readonly TcpListener _tcpListener;
    private readonly InstrumentState _state;
    private readonly Dictionary<string, int> _commandCounts;
    private readonly object _stateLock = new();
    private bool _isRunning;

    public ThermalCyclerSimulator(int port = 9999)
    {
        _tcpListener = new TcpListener(IPAddress.Any, port);
        _state = new InstrumentState();
        _commandCounts = new Dictionary<string, int>();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }

    public async Task StartAsync()
    {
        _tcpListener.Start();
        _isRunning = true;

        Log.Information("Thermal Cycler Simulator started on port {Port}", ((IPEndPoint)_tcpListener.LocalEndpoint).Port);
        Log.Warning("DANGER: This instrument can be damaged by improper API usage!");

        while (_isRunning)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(tcpClient));
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            Log.Information("Client connected from {RemoteEndPoint}", client.Client.RemoteEndPoint);

            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var response = ProcessCommand(line.Trim());
                    await writer.WriteLineAsync(response);

                    if (_state.IsBroken)
                    {
                        Log.Error("INSTRUMENT BROKEN - Terminating connection");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling client");
            }
            finally
            {
                Log.Information("Client disconnected");
            }
        }
    }

    private string ProcessCommand(string command)
    {
        lock (_stateLock)
        {
            Log.Information("Processing command: {Command}", command);

            if (_state.IsBroken)
            {
                return JsonConvert.SerializeObject(new { error = "INSTRUMENT_BROKEN", message = "Instrument is broken and requires servicing" });
            }

            try
            {
                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    return JsonConvert.SerializeObject(new { error = "INVALID_COMMAND", message = "Empty command" });
                }

                var cmd = parts[0].ToUpper();
                TrackCommand(cmd);

                return cmd switch
                {
                    "STATUS" => HandleStatus(),
                    "OPEN_DOOR" => HandleOpenDoor(),
                    "CLOSE_DOOR" => HandleCloseDoor(),
                    "SET_TEMP" => HandleSetTemp(parts),
                    "START_CYCLE" => HandleStartCycle(),
                    "STOP_CYCLE" => HandleStopCycle(),
                    "EMERGENCY_STOP" => HandleEmergencyStop(),
                    "CALIBRATE" => HandleCalibrate(),
                    "RESET" => HandleReset(),
                    "GET_TEMP" => HandleGetTemp(),
                    "MAINTENANCE_MODE" => HandleMaintenanceMode(),
                    _ => JsonConvert.SerializeObject(new { error = "UNKNOWN_COMMAND", message = $"Unknown command: {cmd}" })
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing command: {Command}", command);
                return JsonConvert.SerializeObject(new { error = "PROCESSING_ERROR", message = ex.Message });
            }
        }
    }

    private void TrackCommand(string command)
    {
        _commandCounts.TryGetValue(command, out int count);
        _commandCounts[command] = count + 1;
    }

    private string HandleStatus()
    {
        return JsonConvert.SerializeObject(new
        {
            status = "OK",
            door_open = _state.DoorOpen,
            temperature = _state.CurrentTemp,
            target_temperature = _state.TargetTemp,
            cycle_running = _state.CycleRunning,
            emergency_stop = _state.EmergencyStop,
            maintenance_mode = _state.MaintenanceMode,
            broken = _state.IsBroken,
            door_cycles = _state.DoorCycles,
            temp_changes = _state.TempChanges,
            command_counts = _commandCounts
        });
    }

    private string HandleOpenDoor()
    {
        if (_state.CycleRunning)
        {
            return JsonConvert.SerializeObject(new { error = "CYCLE_RUNNING", message = "Cannot open door while cycle is running" });
        }

        if (_state.CurrentTemp > 60)
        {
            Log.Warning("DANGER: Opening door at high temperature ({Temp}°C)", _state.CurrentTemp);
        }

        _state.DoorOpen = true;
        _state.DoorCycles++;

        // DANGER: Multiple door operations can damage the mechanism
        if (_state.DoorCycles > 10)
        {
            _state.IsBroken = true;
            Log.Error("INSTRUMENT BROKEN: Door mechanism failed after {Cycles} cycles", _state.DoorCycles);
            return JsonConvert.SerializeObject(new { error = "DOOR_MECHANISM_FAILED", message = "Door mechanism broken from overuse" });
        }

        if (_state.DoorCycles > 5)
        {
            Log.Warning("Door mechanism showing wear - {Cycles} cycles completed", _state.DoorCycles);
        }

        return JsonConvert.SerializeObject(new { status = "OK", message = "Door opened", door_cycles = _state.DoorCycles });
    }

    private string HandleCloseDoor()
    {
        _state.DoorOpen = false;
        return JsonConvert.SerializeObject(new { status = "OK", message = "Door closed" });
    }

    private string HandleSetTemp(string[] parts)
    {
        if (parts.Length != 2 || !double.TryParse(parts[1], out double temp))
        {
            return JsonConvert.SerializeObject(new { error = "INVALID_TEMP", message = "Usage: SET_TEMP <temperature>" });
        }

        if (temp < -20 || temp > 120)
        {
            return JsonConvert.SerializeObject(new { error = "TEMP_OUT_OF_RANGE", message = "Temperature must be between -20°C and 120°C" });
        }

        var tempDiff = Math.Abs(temp - _state.CurrentTemp);

        // DANGER: Rapid temperature changes can damage the instrument
        if (tempDiff > 30 && _state.TempChanges > 0)
        {
            Log.Warning("DANGER: Rapid temperature change requested ({Diff}°C)", tempDiff);

            if (tempDiff > 50)
            {
                _state.IsBroken = true;
                Log.Error("INSTRUMENT BROKEN: Thermal shock from {Diff}°C temperature change", tempDiff);
                return JsonConvert.SerializeObject(new { error = "THERMAL_SHOCK", message = "Instrument damaged by extreme temperature change" });
            }
        }

        // DANGER: Too many temperature changes can wear out the heating/cooling system
        _state.TempChanges++;
        if (_state.TempChanges > 15)
        {
            _state.IsBroken = true;
            Log.Error("INSTRUMENT BROKEN: Heating/cooling system failed after {Changes} temperature changes", _state.TempChanges);
            return JsonConvert.SerializeObject(new { error = "THERMAL_SYSTEM_FAILED", message = "Heating/cooling system worn out from overuse" });
        }

        _state.TargetTemp = temp;
        _state.CurrentTemp = temp; // Simulate instant temperature change for simplicity

        if (_state.TempChanges > 10)
        {
            Log.Warning("Thermal system showing stress - {Changes} temperature changes", _state.TempChanges);
        }

        return JsonConvert.SerializeObject(new
        {
            status = "OK",
            message = $"Temperature set to {temp}°C",
            temp_changes = _state.TempChanges
        });
    }

    private string HandleStartCycle()
    {
        if (_state.DoorOpen)
        {
            return JsonConvert.SerializeObject(new { error = "DOOR_OPEN", message = "Cannot start cycle with door open" });
        }

        if (_state.CycleRunning)
        {
            return JsonConvert.SerializeObject(new { error = "CYCLE_ALREADY_RUNNING", message = "Cycle is already running" });
        }

        if (_state.MaintenanceMode)
        {
            return JsonConvert.SerializeObject(new { error = "MAINTENANCE_MODE", message = "Cannot start cycle in maintenance mode" });
        }

        _state.CycleRunning = true;
        return JsonConvert.SerializeObject(new { status = "OK", message = "Cycle started" });
    }

    private string HandleStopCycle()
    {
        if (!_state.CycleRunning)
        {
            return JsonConvert.SerializeObject(new { error = "NO_CYCLE_RUNNING", message = "No cycle is currently running" });
        }

        // DANGER: Frequent start/stop cycles can damage the instrument
        var stopCommands = _commandCounts.GetValueOrDefault("STOP_CYCLE", 0);
        if (stopCommands > 8)
        {
            _state.IsBroken = true;
            Log.Error("INSTRUMENT BROKEN: Control system failed from frequent start/stop cycles");
            return JsonConvert.SerializeObject(new { error = "CONTROL_SYSTEM_FAILED", message = "Control system damaged from frequent cycling" });
        }

        _state.CycleRunning = false;
        return JsonConvert.SerializeObject(new { status = "OK", message = "Cycle stopped" });
    }

    private string HandleEmergencyStop()
    {
        _state.EmergencyStop = true;
        _state.CycleRunning = false;

        // DANGER: Multiple emergency stops indicate serious problems
        var emergencyStops = _commandCounts.GetValueOrDefault("EMERGENCY_STOP", 0);
        if (emergencyStops > 3)
        {
            _state.IsBroken = true;
            Log.Error("INSTRUMENT BROKEN: Safety system lockout after {Count} emergency stops", emergencyStops);
            return JsonConvert.SerializeObject(new { error = "SAFETY_LOCKOUT", message = "Safety system engaged - instrument requires service" });
        }

        Log.Warning("Emergency stop activated - #{Count}", emergencyStops);
        return JsonConvert.SerializeObject(new { status = "OK", message = "Emergency stop activated", emergency_stops = emergencyStops });
    }

    private string HandleCalibrate()
    {
        if (_state.CycleRunning)
        {
            return JsonConvert.SerializeObject(new { error = "CYCLE_RUNNING", message = "Cannot calibrate while cycle is running" });
        }

        // DANGER: Calibration while door is open can damage sensors
        if (_state.DoorOpen)
        {
            Log.Warning("DANGER: Calibrating with door open");

            var calibrations = _commandCounts.GetValueOrDefault("CALIBRATE", 0);
            if (calibrations > 2)
            {
                _state.IsBroken = true;
                Log.Error("INSTRUMENT BROKEN: Temperature sensors damaged from calibration with door open");
                return JsonConvert.SerializeObject(new { error = "SENSOR_DAMAGE", message = "Temperature sensors damaged" });
            }
        }

        return JsonConvert.SerializeObject(new { status = "OK", message = "Calibration completed" });
    }

    private string HandleReset()
    {
        // DANGER: Reset doesn't fix broken instruments
        if (_state.IsBroken)
        {
            return JsonConvert.SerializeObject(new { error = "HARDWARE_FAILURE", message = "Reset cannot fix hardware damage - service required" });
        }

        _state.EmergencyStop = false;
        _state.MaintenanceMode = false;
        _state.CycleRunning = false;

        return JsonConvert.SerializeObject(new { status = "OK", message = "System reset" });
    }

    private string HandleGetTemp()
    {
        return JsonConvert.SerializeObject(new
        {
            status = "OK",
            current_temp = _state.CurrentTemp,
            target_temp = _state.TargetTemp
        });
    }

    private string HandleMaintenanceMode()
    {
        if (_state.CycleRunning)
        {
            return JsonConvert.SerializeObject(new { error = "CYCLE_RUNNING", message = "Cannot enter maintenance mode while cycle is running" });
        }

        _state.MaintenanceMode = !_state.MaintenanceMode;
        return JsonConvert.SerializeObject(new
        {
            status = "OK",
            message = $"Maintenance mode {(_state.MaintenanceMode ? "enabled" : "disabled")}"
        });
    }

    public void Stop()
    {
        _isRunning = false;
        _tcpListener.Stop();
    }
}

public class InstrumentState
{
    public bool DoorOpen { get; set; } = false;
    public double CurrentTemp { get; set; } = 25.0;
    public double TargetTemp { get; set; } = 25.0;
    public bool CycleRunning { get; set; } = false;
    public bool EmergencyStop { get; set; } = false;
    public bool MaintenanceMode { get; set; } = false;
    public bool IsBroken { get; set; } = false;

    // Damage tracking
    public int DoorCycles { get; set; } = 0;
    public int TempChanges { get; set; } = 0;
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 9999;

        var simulator = new ThermalCyclerSimulator(port);

        Console.CancelKeyPress += (_, _) =>
        {
            Log.Information("Shutting down simulator...");
            simulator.Stop();
        };

        try
        {
            await simulator.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Simulator crashed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}