using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyTitle("Projector C# Copier")]
[assembly: AssemblyDescription("Makes a copy of a project, changing the core project file names and GUIDs")]
[assembly: AssemblyCompany("John Lyon-Smith")]
[assembly: AssemblyProduct(".NET Coding Tools")]
[assembly: AssemblyCopyright("Copyright (c) John Lyon-Smith 2013")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.10825.0")]