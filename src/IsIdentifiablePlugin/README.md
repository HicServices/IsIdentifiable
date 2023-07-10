# IsIdentifiable Plugin

Plugin for [RDMP](https://github.com/HicServices/RDMP) that adds support for running IsIdentifiable on Catalogues through the command line and/or gui client.

# Building

Building requires MSBuild 15 or later (or Visual Studio 2017 or later). You will also need to install the [net6.0 SDK](https://dotnet.microsoft.com/download).

You can build IsIdentifiablePlugin by running the following (use the Version number in [SharedAssemblyInfo.cs](../SharedAssemblyInfo.cs) in place of 0.0.1)

```bash
cd IsIdentifiablePlugin
dotnet publish --self-contained false
nuget pack ./IsIdentifiablePlugin.nuspec -Properties Configuration=Debug -IncludeReferencedProjects -Symbols -Version 0.0.1
```

This will produce a nupkg file (e.g. IsIdentifiablePlugin.0.0.1.nupkg) which can be consumed by both the RDMP client and dot net core RDMP CLI.
