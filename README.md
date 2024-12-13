# OmronFinsNetStandard

[![BUILD](https://github.com/volkovskey/OmronFinsNetStandard/actions/workflows/dotnet.yml/badge.svg)](BUILD)
[![NuGet](https://img.shields.io/nuget/v/OmronFinsNetStandard.svg)](https://www.nuget.org/packages/OmronFinsNetStandard/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
  - [Connecting to the PLC](#connecting-to-the-plc)
  - [Reading Bits](#reading-bits)
  - [Reading Words](#reading-words)
  - [Reading Real Values](#reading-real-values)
  - [Exception Handling (FinsError)](#exception-handling-finserror)
  - [Closing the Connection](#closing-the-connection)
- [Logging](#logging)
  - [Enabling Logs in NLog](#enabling-logs-in-nlog)
- [API Reference](#api-reference)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Introduction

**OmronFinsNetStandard** is a .NET Standard library that provides a straightforward and reliable way to communicate with Omron PLCs using the FINS (Factory Interface Network Service) protocol over Ethernet. Whether you're building industrial automation solutions, integrating PLC data into your software, or performing diagnostics, this library streamlines common tasks such as connecting, reading, and writing to Omron PLC memory areas.

## Features

- **Asynchronous Operations:** Perform non-blocking I/O operations when interacting with the PLC.
- **Robust Error Handling:** The library throws `FinsError` exceptions when communication issues or PLC-side errors occur, allowing you to handle these gracefully.
- **Logging with NLog:** Integrated logging uses the popular NLog framework for diagnostics and audit trails.
- **Dependency Injection-Friendly:** Easily integrate into DI containers for more modular and testable code.

## Installation

You can install the `OmronFinsNetStandard` package from [NuGet](https://www.nuget.org/):

```bash
dotnet add package OmronFinsNetStandard
```

Or via the NuGet Package Manager in Visual Studio:

```powershell
Install-Package OmronFinsNetStandard
```

## Usage

### Connecting to the PLC

```csharp
using OmronFinsNetStandard;

var client = new EthernetPlcClient();
// Attempt to connect to the PLC at the given IP and port
bool isConnected = await client.ConnectAsync("192.168.1.10", 9600, timeout: 3000);

if (isConnected)
{
    Console.WriteLine("Successfully connected to the PLC.");
}
else
{
    Console.WriteLine("Failed to connect to the PLC.");
}
```

### Reading Bits

```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
string bitAddress = "100.5"; // Format: "Word.Bit"

// Reading the state of a single bit
try
{
    short bitState = await client.GetBitStateAsync(memory, bitAddress);
    Console.WriteLine($"Bit State at DM100.5: {bitState}");
}
catch (FinsError ex)
{
    Console.WriteLine($"Failed to read bit state: {ex.Message}");
}
```

### Reading Words

```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
ushort startAddress = 200; 
ushort wordCount = 3; // Number of words to read

try
{
    short[] words = await client.ReadWordsAsync(memory, startAddress, wordCount);
    Console.WriteLine("Read words from DM200:");
    foreach (var word in words)
    {
        Console.WriteLine(word);
    }
}
catch (FinsError ex)
{
    // Handle PLC communication errors gracefully
    Console.WriteLine($"Error reading words: MainCode={ex.MainCode}, SubCode={ex.SubCode}, Message={ex.Message}");
}
```

### Reading Real Values

```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
ushort realAddress = 300; 
// Each float is 2 words, so ensure you read the correct range

try
{
    float realValue = await client.ReadRealAsync(memory, realAddress);
    Console.WriteLine($"Real Value at DM300: {realValue}");
}
catch (FinsError ex)
{
    Console.WriteLine($"Error reading real value: {ex.Message}");
}
```

### Exception Handling (FinsError)

Because network issues, PLC configuration problems, and other factors can cause read/write operations to fail, the library throws `FinsError` exceptions. These exceptions provide detailed error codes and messages.

**Example:**

```csharp
try
{
    short[] data = await client.ReadWordsAsync(PlcMemory.DM, 0, 10);
    Console.WriteLine("Data read successfully.");
}
catch (FinsError ex)
{
    Console.WriteLine($"A FinsError occurred! Message: {ex.Message}");
    Console.WriteLine($"MainCode: {ex.MainCode}, SubCode: {ex.SubCode}");
    Console.WriteLine("Consider checking the PLC configuration or network connectivity.");
}
```

### Closing the Connection

```csharp
await client.CloseAsync();
Console.WriteLine("Disconnected from the PLC.");
```

## Logging

**OmronFinsNetStandard** uses [NLog](https://nlog-project.org/) to record diagnostic information, errors, and other events. If you wish to see logs in your application, you must configure NLog accordingly.

### Enabling Logs in NLog

1. Add an `NLog.config` file to your application (or integrate into an existing NLog configuration).
2. Include a rule that captures logs from all namespaces (or specifically `OmronFinsNetStandard`) so that library logs are recorded.

**Example NLog.config:**

```xml
<?xml version="1.0" encoding="utf-8"?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <!-- Write logs to a file -->
    <target xsi:type="File" name="file" fileName="logs/logfile.log"
            layout="${longdate} ${uppercase:${level}} ${message} ${exception:format=toString}" />
  </targets>

  <rules>
    <!-- Capture all logs at Info level and above -->
    <logger name="*" minlevel="Info" writeTo="file" />
  </rules>
</nlog>
```

**Important:** Make sure the `NLog.config` file is included in your project output (e.g., by setting `Copy to Output Directory` to `Copy if newer` in Visual Studio). Without proper configuration, you will not see any log output from this library.

## API Reference

### EthernetPlcClient

**Constructor:**
```csharp
public EthernetPlcClient()
```

**Key Methods:**
- `Task<bool> ConnectAsync(string ipAddress, int port, int timeout)`  
  Connects to the PLC asynchronously.
  
- `Task<short> GetBitStateAsync(PlcMemory memory, string address)`  
  Reads the state of a specific bit from the PLC.

- `Task<short[]> ReadWordsAsync(PlcMemory memory, ushort address, ushort count)`  
  Reads multiple words from the PLC.

- `Task<float> ReadRealAsync(PlcMemory memory, ushort address)`  
  Reads a real (float) value from the PLC.

- `Task CloseAsync()`  
  Closes the connection to the PLC.

- `void Dispose()`  
  Disposes resources used by the client.

### FinsError

`FinsError` is a custom exception that provides detailed information on PLC communication errors.

**Properties:**
- `byte MainCode`  
  The main error code returned by the PLC.
  
- `byte SubCode`  
  The sub error code for more granular error details.

- `bool CanContinue`  
  Indicates whether communication can continue after this error.

- `override string Message`  
  A descriptive error message.

## Contributing

Contributions, bug reports, and feature requests are welcome. To contribute:

1. **Fork the repository** on GitHub.
2. **Create a feature branch** for your changes.
3. **Commit and push your changes**.
4. **Open a Pull Request** and describe the changes and their rationale.

## License

This project is licensed under the [MIT License](LICENSE).

## Contact

For questions, suggestions, or issues, please open an [issue on GitHub](https://github.com/volkovskey/OmronFinsNetStandard).

---

*Happy coding!*