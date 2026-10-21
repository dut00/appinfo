namespace Dut00.AppInfo.Runtime.Internal;

/// <summary>
/// The <c>Runtime</c> group. Properties are serialized in declaration order.
/// </summary>
/// <param name="Framework">The .NET runtime, for example <c>.NET 10.0.0</c>.</param>
/// <param name="RuntimeIdentifier">The runtime identifier, for example <c>linux-x64</c>.</param>
/// <param name="OSDescription">The operating system, for example <c>Ubuntu 24.04.1 LTS</c>.</param>
/// <param name="OSArchitecture">The operating system architecture as an enum name, for example <c>X64</c>.</param>
/// <param name="ProcessArchitecture">The process architecture as an enum name, for example <c>Arm64</c>.</param>
/// <remarks>
/// Architectures are strings, not enums: System.Text.Json writes enums as numbers, and an app's
/// <c>JsonStringEnumConverter</c> could change their casing.
/// </remarks>
internal sealed record RuntimeDetails(
    string Framework,
    string RuntimeIdentifier,
    string OSDescription,
    string OSArchitecture,
    string ProcessArchitecture);
