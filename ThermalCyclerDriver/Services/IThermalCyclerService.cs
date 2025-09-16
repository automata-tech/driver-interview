namespace ThermalCyclerDriver.Services;

public interface IThermalCyclerService
{
    Task<ThermalCyclerStatus> GetStatusAsync();
    Task<ThermalCyclerResponse> OpenDoorAsync();
    Task<ThermalCyclerResponse> CloseDoorAsync();
    Task<ThermalCyclerResponse> SetTemperatureAsync(double temperature);
    Task<ThermalCyclerResponse> StartCycleAsync();
    Task<ThermalCyclerResponse> StopCycleAsync();
    Task<ThermalCyclerResponse> EmergencyStopAsync();
    Task<ThermalCyclerResponse> CalibrateAsync();
    Task<ThermalCyclerResponse> ResetAsync();
}

public class ThermalCyclerStatus
{
    public bool DoorOpen { get; set; }
    public double CurrentTemperature { get; set; }
    public double TargetTemperature { get; set; }
    public bool CycleRunning { get; set; }
    public bool EmergencyStop { get; set; }
    public bool MaintenanceMode { get; set; }
    public bool IsBroken { get; set; }
    public int DoorCycles { get; set; }
    public int TemperatureChanges { get; set; }
}

public class ThermalCyclerResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
}

// TODO: Implement ThermalCyclerService that:
// 1. Manages TCP connection to localhost:9999
// 2. Implements all the safety logic to prevent instrument damage
// 3. Tracks state to prevent dangerous operations
// 4. Handles connection failures gracefully
// 5. Provides thread-safe operations

// Key safety features to implement:
// - Rate limiting for temperature changes
// - Door cycle counting and limits
// - Emergency stop counting
// - Calibration safety checks
// - Thermal shock prevention
// - Control system protection