using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyTitle("Version Stamper")]
[assembly: AssemblyDescription("Creates version stamps for various types of files in C# projects")]
[assembly: AssemblyCompany("John Lyon-Smith")]
[assembly: AssemblyProduct(".NET Coding Tools")]
[assembly: AssemblyCopyright("Copyright © John Lyon-Smith 2013")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.10825.0")]