using System.Reflection;

[assembly: AssemblyTitle("TVFollow")]
[assembly: AssemblyDescription("Turns TVs with Roku built in on and off along with this PC's monitors.")]
[assembly: AssemblyCompany("b0r3d.org")]
[assembly: AssemblyProduct("TVFollow")]
[assembly: AssemblyCopyright("Copyright (c) 2026 still b0r3d. MIT License with the Commons Clause: free, not for sale.")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyInformationalVersion("1.2")]

// the compiler that comes with Windows leaves this out, and without it .NET runs the exe in its
// .NET 4.0 compatibility mode: web addresses get "cleaned up" (a typed . / or \ never reached the
// TV as typed), TLS 1.2 isn't the default, and older XML defaults apply. Visual Studio always
// writes it; the windows draw the same either way.
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
