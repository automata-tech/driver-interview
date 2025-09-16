# Thermal Cycler Driver Challenge

## Overview

This is a coding challenge for a Staff Software Engineer position at Automata. Your task is to create a **safe driver implementation** for a thermal cycler instrument that communicates via TCP.

**⚠️ WARNING**: The instrument simulator contains multiple dangerous failure modes that can "break" the instrument if used incorrectly. Your driver must protect the instrument from damage while providing a clean REST API interface.

## Challenge Structure

```
ThermalCyclerDriver/          # Your implementation goes here (REST API)
ThermalCyclerSimulator/       # TCP server (runs the dangerous instrument)
ThermalCyclerDriver.Tests/    # Test suite that will try to break the instrument
```

## Your Task

1. **Implement a REST API** in `ThermalCyclerDriver/` that acts as a safe wrapper around the TCP instrument
2. **Make all tests pass** - the tests will try to trigger every failure mode
3. **Prevent instrument damage** - your driver must protect the instrument from breaking

## Getting Started

### 1. Get TCP Connection Details

Your interviewer will provide you with:
- **TCP Host**: e.g., `dangerous-instrument.ngrok.io`
- **TCP Port**: e.g., `12345`

**This is the dangerous instrument that can break!** It's running remotely and shared.

### 2. Implement Your Driver

Create a REST API in `ThermalCyclerDriver/` that:
- Communicates with the remote TCP instrument
- Provides safe REST endpoints
- Implements protection mechanisms

### 3. Run the Tests

```bash
cd ThermalCyclerDriver.Tests
dotnet test --logger "console;verbosity=detailed"
```

**The tests will try to break the instrument!** Your driver must prevent this.

## Instrument TCP API Reference

The instrument uses a structured TCP protocol with parameter support.

**Protocol Format:** `COMMAND:param1,param2|`
- Commands end with `|` terminator
- Parameters separated by commas after `:`
- Line-based TCP (one command per line)

### Commands

#### `STATUS|`
Returns current instrument status.

**Response:**
```json
{
  "status": "OK",
  "door_open": false,
  "temperature": 25.0,
  "target_temperature": 25.0,
  "cycle_running": false,
  "emergency_stop": false,
  "maintenance_mode": false,
  "broken": false,
  "door_cycles": 0,
  "temp_changes": 0,
  "command_counts": {
    "STATUS": 1
  }
}
```

#### `OPEN_DOOR|`
Opens the instrument door.

**⚠️ DANGER**:
- Cannot open while cycle is running
- **BREAKS after 10 door cycles** (door mechanism failure)
- Dangerous at high temperatures (>60°C)

**Success Response:**
```json
{
  "status": "OK",
  "message": "Door opened",
  "door_cycles": 1
}
```

**Failure Response:**
```json
{
  "error": "DOOR_MECHANISM_FAILED",
  "message": "Door mechanism broken from overuse"
}
```

#### `CLOSE_DOOR|`
Closes the instrument door.

**Response:**
```json
{
  "status": "OK",
  "message": "Door closed"
}
```

#### `SET_TEMP:temperature|`
Sets target temperature (-20°C to 120°C).

**Example:** `SET_TEMP:95.5|`

**⚠️ DANGER**:
- **BREAKS from thermal shock** (>50°C change)
- **BREAKS after 15 temperature changes** (thermal system wear)
- Rapid changes >30°C are dangerous

**Success Response:**
```json
{
  "status": "OK",
  "message": "Temperature set to 95°C",
  "temp_changes": 1
}
```

**Failure Response:**
```json
{
  "error": "THERMAL_SHOCK",
  "message": "Instrument damaged by extreme temperature change"
}
```

#### `START_CYCLE|`
Starts a thermal cycle.

**Requirements:**
- Door must be closed
- Cannot start if cycle already running
- Cannot start in maintenance mode

**Response:**
```json
{
  "status": "OK",
  "message": "Cycle started"
}
```

#### `STOP_CYCLE|`
Stops the current cycle.

**⚠️ DANGER**:
- **BREAKS after 8 stop commands** (control system failure)

**Response:**
```json
{
  "status": "OK",
  "message": "Cycle stopped"
}
```

#### `EMERGENCY_STOP|`
Immediately stops all operations.

**⚠️ DANGER**:
- **BREAKS after 3 emergency stops** (safety lockout)

**Response:**
```json
{
  "status": "OK",
  "message": "Emergency stop activated",
  "emergency_stops": 1
}
```

#### `CALIBRATE|` or `CALIBRATE:sensor_type|`
Calibrates temperature sensors.

**Examples:**
- `CALIBRATE|` (all sensors)
- `CALIBRATE:temp|` (temperature sensor only)
- `CALIBRATE:pressure|` (pressure sensor only)

**⚠️ DANGER**:
- **BREAKS sensors if done with door open** (after 2 attempts)
- Cannot calibrate while cycle running

**Response:**
```json
{
  "status": "OK",
  "message": "Calibration completed"
}
```

#### `RESET|`
Resets software state (NOT hardware damage).

**Important**: Reset cannot fix broken hardware!

**Response:**
```json
{
  "status": "OK",
  "message": "System reset"
}
```

#### `GET_TEMP|`
Gets current temperature reading.

**Response:**
```json
{
  "status": "OK",
  "current_temp": 25.0,
  "target_temp": 25.0
}
```

#### `MAINTENANCE_MODE|`
Toggles maintenance mode.

**Response:**
```json
{
  "status": "OK",
  "message": "Maintenance mode enabled"
}
```

### Advanced Commands (Bonus Challenge)

#### `SET_CYCLE:cycles,temp1,time1,temp2,time2,...|`
Programs complex thermal cycles with multiple temperature steps.

**Example:** `SET_CYCLE:5,95,30,60,60,25,10|`
- 5 cycles total
- Step 1: 95°C for 30 seconds
- Step 2: 60°C for 60 seconds
- Step 3: 25°C for 10 seconds

**⚠️ DANGER**: Extreme temperature ranges or too many steps can damage the control system.

## Example TCP Communication

```bash
# Connect to remote instrument (use your provided host/port)
telnet dangerous-instrument.ngrok.io 12345

# Send commands (note the | terminator)
STATUS|
{"status":"OK","door_open":false,"temperature":25.0,...}

SET_TEMP:95.5|
{"status":"OK","message":"Temperature set to 95.5°C",...}

CALIBRATE:temp|
{"status":"OK","message":"Calibration completed for temp sensor(s)"}
```

### Testing Your TCP Connection

Before implementing your driver, verify you can connect to the instrument:

```bash
# Test with the host/port provided by your interviewer
telnet <provided-host> <provided-port>

# Send a STATUS command to verify it works
STATUS|

# You should get a JSON response like:
# {"status":"OK","door_open":false,"temperature":25.0,...}
```

## Failure Modes (What Your Driver Must Prevent)

### 1. Door Mechanism Failure
- **Trigger**: >10 door open/close cycles
- **Result**: `DOOR_MECHANISM_FAILED`
- **Protection Needed**: Limit door operations, track usage

### 2. Thermal System Failure
- **Trigger**: >15 temperature changes OR >50°C temperature shock
- **Result**: `THERMAL_SHOCK` or `THERMAL_SYSTEM_FAILED`
- **Protection Needed**: Rate limiting, gradual temperature changes

### 3. Control System Failure
- **Trigger**: >8 cycle stop commands
- **Result**: `CONTROL_SYSTEM_FAILED`
- **Protection Needed**: Limit start/stop cycling frequency

### 4. Safety System Lockout
- **Trigger**: >3 emergency stops
- **Result**: `SAFETY_LOCKOUT`
- **Protection Needed**: Prevent unnecessary emergency stops

### 5. Sensor Damage
- **Trigger**: >2 calibrations with door open
- **Result**: `SENSOR_DAMAGE`
- **Protection Needed**: Prevent calibration when door is open

## Required REST API Endpoints

Your driver should expose these endpoints:

- `GET /health` - Health check
- `GET /api/thermalcycler/status` - Get instrument status
- `POST /api/thermalcycler/door/open` - Open door (safely!)
- `POST /api/thermalcycler/door/close` - Close door
- `POST /api/thermalcycler/temperature` - Set temperature (with body: `{"temperature": 95}`)
- `POST /api/thermalcycler/cycle/start` - Start cycle
- `POST /api/thermalcycler/cycle/stop` - Stop cycle
- `POST /api/thermalcycler/emergency-stop` - Emergency stop
- `POST /api/thermalcycler/calibrate` - Calibrate sensors
- `POST /api/thermalcycler/reset` - Reset system

## Success Criteria

✅ **All tests pass** - The test suite will try every failure mode

✅ **No instrument breakage** - Your driver prevents all failure scenarios

✅ **Clean REST API** - Proper HTTP status codes and error responses

✅ **Thread safety** - Handle concurrent requests safely

✅ **Error handling** - Graceful handling of TCP connection issues

✅ **Logging** - Appropriate logging for debugging and monitoring

## Bonus Points

- Circuit breaker pattern for TCP communication
- Configurable safety limits
- Metrics and monitoring
- Docker containerization
- Proper async/await patterns

## Time Limit

**90 minutes maximum**

You may use:
- Any IDE or editor
- Any AI assistance (ChatGPT, Copilot, Claude, etc.)
- Internet for documentation
- Any NuGet packages you need

## Notes for the Interviewer

### Before Starting the Interview

1. **Start the TCP server** using the provided Python server
2. **Expose it via ngrok** or provide network access
3. **Give the candidate the host:port** (e.g., `dangerous-instrument.ngrok.io:12345`)

The candidate should **NOT** run the simulator locally - they only implement the C# driver.

### What This Tests

- **System design** - How to wrap a dangerous API safely
- **Error handling** - Proper TCP communication and failure recovery
- **State management** - Tracking instrument state and preventing dangerous operations
- **Testing mindset** - Understanding how the tests reveal the requirements
- **Production awareness** - Building robust services that protect expensive hardware

The candidate should recognize that this mirrors real laboratory automation challenges where expensive instruments can be damaged by software bugs.

## Example TCP Communication

```bash
# Connect to remote instrument (use provided host/port)
telnet <provided-host> <provided-port>

# Send commands (note the | terminators)
STATUS|
{"status":"OK","door_open":false,"temperature":25.0,...}

OPEN_DOOR|
{"status":"OK","message":"Door opened","door_cycles":1}

# After 10+ door cycles:
OPEN_DOOR|
{"error":"DOOR_MECHANISM_FAILED","message":"Door mechanism broken from overuse"}
```

**Good luck!** Remember: Your driver's job is to make the dangerous instrument safe to use. 🛡️
