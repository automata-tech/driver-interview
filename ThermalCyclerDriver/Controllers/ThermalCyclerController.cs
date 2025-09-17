using Microsoft.AspNetCore.Mvc;

namespace ThermalCyclerDriver.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ThermalCyclerController : ControllerBase
{
    private readonly ILogger<ThermalCyclerController> _logger;

    public ThermalCyclerController(ILogger<ThermalCyclerController> logger)
    {
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        // TODO: Implement safe status retrieval from TCP instrument
        _logger.LogInformation("Status request received");

        // This is where you'll communicate with the TCP server hosted by your interviewer
        // Remember to handle connection failures gracefully!

        return StatusCode(500, "TODO: Implement status retrieval");
    }

    [HttpPost("door/open")]
    public IActionResult OpenDoor()
    {
        // TODO: Implement safe door opening
        // DANGER: This can break the instrument after 10 cycles!
        // You need to implement protection logic here

        _logger.LogInformation("Door open request received");
        return StatusCode(500, "TODO: Implement safe door opening");
    }

    [HttpPost("door/close")]
    public IActionResult CloseDoor()
    {
        // TODO: Implement door closing
        _logger.LogInformation("Door close request received");
        return StatusCode(500, "TODO: Implement door closing");
    }

    [HttpPost("temperature")]
    public IActionResult SetTemperature([FromBody] TemperatureRequest request)
    {
        // TODO: Implement safe temperature setting
        // DANGER: Rapid temperature changes can break the instrument!
        // DANGER: Changes >50°C can cause thermal shock!
        // DANGER: More than 15 temperature changes will break the thermal system!

        _logger.LogInformation("Temperature change request: {Temperature}°C", request.Temperature);

        if (request.Temperature < -20 || request.Temperature > 120)
        {
            return BadRequest("Temperature out of range (-20°C to 120°C)");
        }

        return StatusCode(500, "TODO: Implement safe temperature setting");
    }

    [HttpPost("cycle/start")]
    public IActionResult StartCycle()
    {
        // TODO: Implement cycle start
        // Requirements: Door must be closed, no cycle already running

        _logger.LogInformation("Cycle start request received");
        return StatusCode(500, "TODO: Implement cycle start");
    }

    [HttpPost("cycle/stop")]
    public IActionResult StopCycle()
    {
        // TODO: Implement cycle stop
        // DANGER: More than 8 stop commands will break the control system!

        _logger.LogInformation("Cycle stop request received");
        return StatusCode(500, "TODO: Implement safe cycle stop");
    }

    [HttpPost("emergency-stop")]
    public IActionResult EmergencyStop()
    {
        // TODO: Implement emergency stop
        // DANGER: More than 3 emergency stops will trigger safety lockout!

        _logger.LogInformation("Emergency stop request received");
        return StatusCode(500, "TODO: Implement emergency stop protection");
    }

    [HttpPost("calibrate")]
    public IActionResult Calibrate()
    {
        // TODO: Implement calibration
        // DANGER: Calibrating with door open will damage sensors after 2 attempts!

        _logger.LogInformation("Calibration request received");
        return StatusCode(500, "TODO: Implement safe calibration");
    }

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        // TODO: Implement reset
        // Note: Reset cannot fix hardware damage!

        _logger.LogInformation("Reset request received");
        return StatusCode(500, "TODO: Implement reset");
    }
}

public class TemperatureRequest
{
    public double Temperature { get; set; }
}
