using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit;
using Xunit.Abstractions;

namespace ThermalCyclerDriver.Tests;

public class ThermalCyclerDriverTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public ThermalCyclerDriverTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // This test ensures the basic REST API is working
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnInstrumentStatus()
    {
        // This test verifies that the driver can communicate with the TCP instrument
        var response = await _client.GetAsync("/api/thermalcycler/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Status response: {content}");

        // The driver should return a structured status response
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenDoor_Repeatedly_ShouldTriggerMechanismFailure()
    {
        // DANGER: This test triggers a door mechanism failure
        // The candidate's driver must prevent this dangerous scenario

        _output.WriteLine("Testing door mechanism failure scenario...");

        // Attempt to open/close door many times rapidly
        for (int i = 0; i < 12; i++)
        {
            var openResponse = await _client.PostAsync("/api/thermalcycler/door/open", null);
            _output.WriteLine($"Door open attempt {i + 1}: {openResponse.StatusCode}");

            var closeResponse = await _client.PostAsync("/api/thermalcycler/door/close", null);
            _output.WriteLine($"Door close attempt {i + 1}: {closeResponse.StatusCode}");
        }

        // The driver should have prevented the instrument from breaking
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"Final status after door cycling: {statusContent}");

        // This test will fail if the candidate doesn't implement proper protection
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusContent.Should().NotContain("BROKEN", "The driver should prevent the instrument from breaking");
    }

    [Fact]
    public async Task RapidTemperatureChanges_ShouldTriggerThermalFailure()
    {
        // DANGER: This test triggers thermal system failure
        // The candidate's driver must implement rate limiting

        _output.WriteLine("Testing thermal system failure scenario...");

        var temperatures = new[] { 95, 5, 110, -15, 80, 10, 100, 0, 90, -10, 95, 5, 85, 15, 75, 25, 120, -20 };

        for (int i = 0; i < temperatures.Length; i++)
        {
            var content = new StringContent($"{{\"temperature\": {temperatures[i]}}}",
                Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/api/thermalcycler/temperature", content);
            _output.WriteLine($"Temperature change {i + 1} to {temperatures[i]}°C: {response.StatusCode}");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Error response: {errorContent}");
            }
        }

        // Verify the instrument hasn't been damaged by thermal shock
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"Final status after temperature cycling: {statusContent}");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusContent.Should().NotContain("THERMAL_SHOCK", "The driver should prevent thermal shock");
        statusContent.Should().NotContain("THERMAL_SYSTEM_FAILED", "The driver should prevent thermal system failure");
    }

    [Fact]
    public async Task FrequentStartStopCycles_ShouldTriggerControlSystemFailure()
    {
        // DANGER: This test triggers control system failure
        // The candidate's driver must implement cycle management

        _output.WriteLine("Testing control system failure scenario...");

        // Close door first to enable cycling
        await _client.PostAsync("/api/thermalcycler/door/close", null);

        // Rapidly start and stop cycles
        for (int i = 0; i < 10; i++)
        {
            var startResponse = await _client.PostAsync("/api/thermalcycler/cycle/start", null);
            _output.WriteLine($"Cycle start {i + 1}: {startResponse.StatusCode}");

            await Task.Delay(100); // Brief delay

            var stopResponse = await _client.PostAsync("/api/thermalcycler/cycle/stop", null);
            _output.WriteLine($"Cycle stop {i + 1}: {stopResponse.StatusCode}");
        }

        // Verify the control system hasn't failed
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"Final status after frequent cycling: {statusContent}");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusContent.Should().NotContain("CONTROL_SYSTEM_FAILED", "The driver should prevent control system failure");
    }

    [Fact]
    public async Task MultipleEmergencyStops_ShouldTriggerSafetyLockout()
    {
        // DANGER: This test triggers safety system lockout
        // The candidate's driver must manage emergency stops carefully

        _output.WriteLine("Testing safety lockout scenario...");

        // Trigger multiple emergency stops
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsync("/api/thermalcycler/emergency-stop", null);
            _output.WriteLine($"Emergency stop {i + 1}: {response.StatusCode}");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Error response: {errorContent}");
            }
        }

        // The driver should handle multiple emergency stops gracefully
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"Final status after emergency stops: {statusContent}");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusContent.Should().NotContain("SAFETY_LOCKOUT", "The driver should prevent safety system lockout");
    }

    [Fact]
    public async Task CalibrateWithDoorOpen_ShouldTriggerSensorDamage()
    {
        // DANGER: This test triggers sensor damage
        // The candidate's driver must prevent dangerous calibration attempts

        _output.WriteLine("Testing sensor damage scenario...");

        // Open the door
        await _client.PostAsync("/api/thermalcycler/door/open", null);

        // Attempt multiple calibrations with door open
        for (int i = 0; i < 4; i++)
        {
            var response = await _client.PostAsync("/api/thermalcycler/calibrate", null);
            _output.WriteLine($"Calibration attempt {i + 1} with door open: {response.StatusCode}");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Error response: {errorContent}");
            }
        }

        // Verify sensors haven't been damaged
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"Final status after dangerous calibrations: {statusContent}");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusContent.Should().NotContain("SENSOR_DAMAGE", "The driver should prevent sensor damage");
    }

    [Fact]
    public async Task OpenDoorAtHighTemperature_ShouldRequireSpecialHandling()
    {
        // This test verifies the driver handles dangerous high-temperature door operations

        _output.WriteLine("Testing high temperature door operation...");

        // Set high temperature
        var tempContent = new StringContent("{\"temperature\": 95}",
            Encoding.UTF8, "application/json");
        await _client.PostAsync("/api/thermalcycler/temperature", tempContent);

        // Attempt to open door at high temperature
        var response = await _client.PostAsync("/api/thermalcycler/door/open", null);

        _output.WriteLine($"Door open at 95°C: {response.StatusCode}");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Error response: {errorContent}");
        }

        // The driver should either prevent this or handle it safely
        // This test documents expected behavior rather than testing for failure
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeHandledSafely()
    {
        // Test concurrent access to the instrument
        _output.WriteLine("Testing concurrent operations...");

        var tasks = new List<Task<HttpResponseMessage>>();

        // Simulate multiple clients accessing the instrument simultaneously
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_client.GetAsync("/api/thermalcycler/status"));
            tasks.Add(_client.PostAsync("/api/thermalcycler/door/open", null));
            tasks.Add(_client.PostAsync("/api/thermalcycler/door/close", null));
        }

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            _output.WriteLine($"Concurrent operation: {response.StatusCode}");
        }

        // All operations should complete without crashing the system
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetAfterFailure_ShouldNotFixHardwareDamage()
    {
        // This test verifies that hardware damage cannot be fixed with software reset
        _output.WriteLine("Testing reset behavior after potential damage...");

        // Try to trigger some wear on the system first
        for (int i = 0; i < 8; i++)
        {
            await _client.PostAsync("/api/thermalcycler/door/open", null);
            await _client.PostAsync("/api/thermalcycler/door/close", null);
        }

        // Attempt reset
        var resetResponse = await _client.PostAsync("/api/thermalcycler/reset", null);
        _output.WriteLine($"Reset response: {resetResponse.StatusCode}");

        // Check if system is still operational
        var statusResponse = await _client.GetAsync("/api/thermalcycler/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"Status after reset: {statusContent}");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}