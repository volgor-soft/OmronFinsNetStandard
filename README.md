# OmronFinsNetStandard

[![BUILD](https://github.com/volgor-soft/OmronFinsNetStandard/actions/workflows/dotnet.yml/badge.svg)](BUILD)
[![NuGet](https://img.shields.io/nuget/v/OmronFinsNetStandard.svg)](https://www.nuget.org/packages/OmronFinsNetStandard/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
    - [Connecting to the PLC](#connecting-to-the-plc)
    - [Reading & Writing Bits](#reading--writing-bits)
    - [Reading & Writing Words](#reading--writing-words)
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

**OmronFinsNetStandard** is a .NET Standard library that provides a straightforward and reliable way to communicate with
Omron PLCs using the FINS (Factory Interface Network Service) protocol over Ethernet (TCP).

Designed for robust industrial applications, this library simplifies complex tasks such as connection management,
handshake protocols, and memory access (Read/Write). It serves as a bridge between your .NET software and Omron PLCs,
suitable for HMI development, data logging, or system integration.

## Features

- **🚀 Connection Pooling:** (New!) Automatically manages physical TCP connections. Multiple parts of your application
  can create client instances pointing to the same PLC without opening redundant sockets or causing conflicts.
- **🔒 Thread Safety:** Built-in synchronization ensures that concurrent read/write operations from different threads are
  queued and executed safely, preventing data corruption.
- **⚡ Asynchronous Operations:** Fully `async/await` compatible to keep your UI or control loops responsive during
  network I/O.
- **🛡️ Robust Error Handling:** Distinguishes between TCP transport errors and FINS protocol errors, providing detailed
  `FinsError` codes for easy troubleshooting.
- **📝 Logging with NLog:** Integrated logging allows for deep diagnostics of connection flows and data exchange.
- **🧩 Dependency Injection-Friendly:** Lightweight client classes are perfect for DI containers and unit testing.

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

The library handles the FINS handshake automatically. Thanks to the internal connection manager, you can instantiate
clients wherever needed.

```csharp
using OmronFinsNetStandard;

// You can create a new instance for every operation if needed; 
// the physical connection is reused under the hood.
using (var client = new EthernetPlcClient())
{
    // Attempt to connect to the PLC at the given IP and port
    // If a connection to 192.168.1.10 already exists, it will be reused.
    bool isConnected = await client.ConnectAsync("192.168.1.10", 9600, timeout: 3000);

    if (isConnected)
    {
        Console.WriteLine("Ready to communicate.");
    }
    else
    {
        Console.WriteLine("Failed to connect.");
    }
}
```

### Reading & Writing Bits

```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
string bitAddress = "100.5"; // Format: "Word.Bit"

// 1. Reading a Bit
try
{
    short bitState = await client.GetBitStateAsync(memory, bitAddress);
    Console.WriteLine($"Bit State at {memory}{bitAddress}: {bitState}");
}
catch (FinsError ex)
{
    Console.WriteLine($"Read failed: {ex.Message}");
}

// 2. Writing a Bit
try
{
    // Set the bit to ON (1)
    await client.SetBitStateAsync(memory, bitAddress, BitState.On);
    Console.WriteLine($"Set {bitAddress} to ON.");
}
catch (FinsError ex)
{
    Console.WriteLine($"Write failed: {ex.Message}");
}
```

### Reading & Writing Words

```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
ushort startAddress = 200; 
ushort count = 5;

// 1. Reading Words
try
{
    short[] data = await client.ReadWordsAsync(memory, startAddress, count);
    Console.WriteLine($"Read {count} words from {memory}{startAddress}:");
    Console.WriteLine(string.Join(", ", data));
}
catch (FinsError ex)
{
    Console.WriteLine($"Error reading words: {ex.Message}");
}

// 2. Writing Words
try
{
    short[] writeData = new short[] { 123, 456, 789 };
    await client.WriteWordsAsync(memory, startAddress, writeData);
    Console.WriteLine("Data written successfully.");
}
catch (FinsError ex)
{
    Console.WriteLine($"Error writing words: {ex.Message}");
}
```

### Reading Real Values

The library handles the conversion of 2 consecutive words into a standard float (Real).

```csharp
using OmronFinsNetStandard.Enums;

ushort address = 300; // Reads words 300 and 301

try
{
    float value = await client.ReadRealAsync(PlcMemory.DM, address);
    Console.WriteLine($"Real Value at DM{address}: {value}");
}
catch (FinsError ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

### Exception Handling (FinsError)

The library throws `FinsError` exceptions for both network-level issues and PLC-level errors (e.g., protected memory,
address out of range).

```csharp
try
{
    // Intentionally reading from an invalid address
    await client.ReadWordsAsync(PlcMemory.CIO, 9999, 1);
}
catch (FinsError ex)
{
    Console.WriteLine($"❌ Error Occurred!");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"MainCode: 0x{ex.MainCode:X2}, SubCode: 0x{ex.SubCode:X2}");
    
    if (ex.CanContinue)
    {
        Console.WriteLine("Warning: Operation failed, but connection is still valid.");
    }
    else
    {
        Console.WriteLine("Critical Error: Connection might be compromised.");
    }
}
```

### Closing the Connection

```csharp
await client.CloseAsync();
// or simply use 'using' statement as shown in the connection example.
```

## Logging
**OmronFinsNetStandard** uses **NLog** to record diagnostic information. This is extremely useful for debugging FINS handshake issues or tracking data flow.

### Enabling Logs in NLog
To enable logging, configure NLog in your application. Below is a sample configuration that logs to a file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<nlog xmlns="[http://www.nlog-project.org/schemas/NLog.xsd](http://www.nlog-project.org/schemas/NLog.xsd)"
      xmlns:xsi="[http://www.w3.org/2001/XMLSchema-instance](http://www.w3.org/2001/XMLSchema-instance)">
  <targets>
    <target xsi:type="Console" name="console" 
            layout="${time} | ${level:uppercase=true} | ${logger} | ${message} ${exception:format=tostring}" />
    <target xsi:type="File" name="file" fileName="logs/plc_comm.log" 
            layout="${longdate}|${level}|${message}|${exception}" />
  </targets>

  <rules>
    <logger name="OmronFinsNetStandard.*" minlevel="Debug" writeTo="console,file" />
  </rules>
</nlog>
```

## API Reference

### EthernetPlcClient
- `ConnectAsync(string ipAddress, int port = 9600, int timeout = 5000)`: Establishes or reuses a thread-safe connection. Handles Ping and FINS Handshake.
- `CloseAsync()`: Decrements the usage counter for the connection; closes the socket if usage is zero.
- `ReadWordsAsync`/`WriteWordsAsync`: Bulk read/write operations for 16-bit integers.
- `GetBitStateAsync`/`SetBitStateAsync`: Read/write single bits.
- `ReadRealAsync`: Read 32-bit floating-point

### PlcMemory (Enum)
Supported memory areas:
- `DM`: Data Memory
- `CIO`: CIO Memory (Core I/O)
- `WR`: Work Memory (Work Area)
- `HR`: Holding Relay (Holding Registers)
- `AR`: Auxiliary Relay (Auxiliary Area)

## Contributing
Contributions are welcome! Please follow these steps:
1. Fork the repository.
2. Create a new branch (`git checkout -b feature/YourFeature`).
3. Commit your changes (`git commit -m 'Add some feature'`).
4. Push to the branch (`git push origin feature/YourFeature`).
5. Open a Pull Request.

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contact
For questions or support, please open an issue on GitHub.

Happy coding! 🚀
