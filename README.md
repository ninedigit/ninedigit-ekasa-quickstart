# NineDigit Portos eKasa Quick start

Quick start demo app for Portos eKasa library

## Requirements

Before running the quick start app, please make sure that you have:

1. installed Microsoft Visual studio 2017 or newer with .NET Core cross-platform development toolset. Click here to [download Visual Studio](https://visualstudio.microsoft.com/cs/downloads).
2. .NET Core runtime installed on your machine.
3. given access to Portos eKasa NuGet packages
4. connected protected storage memory (CHDU) to your computer.
6. connected receipt printer to the protected storage memory (CHDU).
5. configured the protected storage memory with our Portos eKasa servis application.

Please feel free to contact us at `info@ninedigit.sk` to help you getting started.

## Troubleshooting

### The `SecureChannelFailure` exception

This error may occur when your application targets older versions of .NET framework. If Portos eKasa application cannot send data to the tax authority eKasa server and exception with "SecureChannelFailure" message is thrown, use code snippet shown below to explicitly configurate secureity protocols (enabling TLS in 1.2) version:

```csharp
ServicePointManager.Expect100Continue = true;
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
```

You can also specify another security protocols this way:

```csharp
ServicePointManager.SecurityProtocol =
  SecurityProtocolType.Tls |
  SecurityProtocolType.Tls11 |
  SecurityProtocolType.Tls12 |
  SecurityProtocolType.Ssl3;
```

### `The type initializer for 'Microsoft.Data.Sqlite.SqliteConnection' threw an exception.`

Please [migrate package config to package reference](https://devblogs.microsoft.com/nuget/migrate-packages-config-to-package-reference/) in your project.

Verify that output folder of your application countains `runtimes` directory with `e_sqlcipher.dll` assemblies. E.g.

```
├───runtimes
│   ├───win-arm
│   │   └───native
│   │           e_sqlcipher.dll
│   │
│   ├───win-x64
│   │   └───native
│   │           e_sqlcipher.dll
│   │
│   └───win-x86
│       └───native
│               e_sqlcipher.dll
```

If `runtimes` directory is not created, remove `<RuntimeIdentifier>win</RuntimeIdentifier>` from `.csproj` file of your application.

### `Could not load file or assembly 'System.IO.Ports, Version 4.0.1.0, Culture...'`

This issue is caused by invalid composition of `System.IO.Ports` NuGet package, used in .NET Framework environmennt ([source](https://github.com/dotnet/runtime/issues/31136)).

When application is throwing an exception with message `Could not load file or assembly 'System.IO.Ports, Version 4.0.1.0, Culture...'` you need to specify binding redirection in the `app.config` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.IO.Ports" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.0.1.0" newVersion="4.0.3.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
```

### `Chyba v podpise dátovej správy`

This issue is caused by invalid configuration of an application. Open settings screen (Nastavenia) of Portos eKasa application and set the "Pokročilé > eKasa klient > prostredie" field to integration or production environment.
