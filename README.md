# OmronFinsNetStandard

[![NuGet](https://img.shields.io/nuget/v/OmronFinsNetStandard.svg)](https://www.nuget.org/packages/OmronFinsNetStandard/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
  - [Connecting to PLC](#connecting-to-plc)
  - [Reading Bits](#reading-bits)
  - [Reading Words](#reading-words)
  - [Reading Real Values](#reading-real-values)
  - [Closing Connection](#closing-connection)
- [API Reference](#api-reference)
- [Unit Testing](#unit-testing)
- [Logging](#logging)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Introduction

**OmronFinsNetStandard** is a .NET Standard library designed to facilitate communication with Omron PLCs using the FINS (Factory Interface Network Service) protocol over Ethernet. It provides a simple and efficient way to connect, read, and write data to Omron PLCs, enabling seamless integration into your .NET applications.

## Features

- **Asynchronous Operations:** Perform non-blocking read and write operations.
- **Error Handling:** Comprehensive error detection and exception handling using custom `FinsError` exceptions.
- **Logging:** Integrated logging with NLog for easy debugging and monitoring.
- **Unit Tested:** Comprehensive unit tests using xUnit and Moq to ensure reliability.
- **Dependency Injection:** Supports dependency injection for better testability and flexibility.

## Installation

You can install the `OmronFinsNetStandard` package via [NuGet](https://www.nuget.org/):

```bash
dotnet add package OmronFinsNetStandard
```

Or via the NuGet Package Manager:

```
Install-Package OmronFinsNetStandard
```

## Usage

### Connecting to PLC

```csharp
using OmronFinsNetStandard;
using OmronFinsNetStandard.Enums;

// Initialize the client with PC Node and PLC Node IDs
var client = new EthernetPlcClient();

// Connect to the PLC
bool isConnected = await client.ConnectAsync("192.168.1.10", 9600, timeout: 3000);

if (isConnected)
{
    Console.WriteLine("Connected to PLC successfully.");
}
else
{
    Console.WriteLine("Failed to connect to PLC.");
}
```

### Reading Bits
```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
string address = "100.5"; // Format: "Word.Bit"

// Read the state of the specified bit
short bitState = await client.GetBitStateAsync(memory, address);

Console.WriteLine($"Bit State: {bitState}");

```

### Reading Words
```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
ushort address = 200; // Starting address
ushort count = 3;     // Number of words to read

try
{
    short[] words = await client.ReadWordsAsync(memory, address, count);
    Console.WriteLine("Read Words:");
    foreach (var word in words)
    {
        Console.WriteLine(word);
    }
}
catch (FinsError ex)
{
    Console.WriteLine($"Error reading words: {ex.Message}");
}
```

### Reading Real Values
```csharp
using OmronFinsNetStandard.Enums;

PlcMemory memory = PlcMemory.DM;
ushort address = 300; // Starting address
ushort count = 2;     // Number of words to read (each float occupies 2 words)

try
{
    float realValue = await client.ReadRealAsync(memory, address);
    Console.WriteLine($"Real Value: {realValue}");
}
catch (FinsError ex)
{
    Console.WriteLine($"Error reading real value: {ex.Message}");
}
```

### Closing Connection
```csharp
// Close the connection when done
await client.CloseAsync();
Console.WriteLine("Disconnected from PLC.");
```

## API Reference

### `EthernetPlcClient`

#### Constructor

```
public EthernetPlcClient(IBasicClass basic = null, IFinsCommandBuilder commandBuilder = null)
```

- **Parameters:**
  - `basic`: (Optional) Custom implementation of `IBasicClass` for dependency injection.
  - `commandBuilder`: (Optional) Custom implementation of `IFinsCommandBuilder` for dependency injection.

#### Methods

- **`Task<bool> ConnectAsync(string ipAddress, int port, int timeout)`**

  Connects to the PLC asynchronously.

- **`Task<short> GetBitStateAsync(PlcMemory memory, string address)`**

  Reads the state of a specific bit from the PLC.

- **`Task<short[]> ReadWordsAsync(PlcMemory memory, ushort address, ushort count)`**

  Reads multiple words from the PLC asynchronously.

- **`Task<float> ReadRealAsync(PlcMemory memory, ushort address)`**

  Reads a real (floating-point) value from the PLC asynchronously.

- **`Task CloseAsync()`**

  Closes the connection to the PLC asynchronously.

- **`void Dispose()`**

  Releases all resources used by the `EthernetPlcClient`.

### `FinsError`

Custom exception thrown when the PLC returns an error.

- **Properties:**
  - `byte MainCode`: Main error code.
  - `byte SubCode`: Sub error code.
  - `string Message`: Error message.
  - `bool CanContinue`: Indicates if the operation can continue despite the error.

## Unit Testing

The library includes comprehensive unit tests using `xUnit` and `Moq` to ensure reliability and correctness without requiring access to a real PLC.

### Running Tests

1. **Navigate to the Test Project Directory:**

```
cd OmronFinsNetStandard.Tests
```

2. **Run the Tests:**

```
dotnet test
```

## Logging

The library uses [NLog](https://nlog-project.org/) for logging various events, errors, and debugging information. Ensure that you have configured NLog in your application to capture and store logs as needed.

### Configuration Example

Create an `NLog.config` file in your project with the following content:

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <!-- the targets to write to -->
  <targets>
    <!-- write logs to file -->
    <target xsi:type="File" name="file" fileName="logs/logfile.log"
            layout="${longdate} ${uppercase:${level}} ${message} ${exception:format=toString}" />
  </targets>

  <!-- rules to map from logger name to target -->
  <rules>
    <logger name="*" minlevel="Info" writeTo="file" />
  </rules>
</nlog>
```

Ensure that the `NLog.config` file is copied to the output directory by setting its properties accordingly in your project.

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the Repository**
2. **Create a Feature Branch**
3. **Commit Your Changes**
4. **Push to the Branch**
5. **Open a Pull Request**

## License

This project is licensed under the [MIT License](LICENSE). See the [LICENSE](LICENSE) file for details.

## Contact

For any questions or suggestions, please open an issue on the [GitHub repository](https://github.com/volkovskey/OmronFinsNetStandard).

---

*Happy Coding! 🚀*
