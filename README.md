# Serilog.Sinks.RavenDB

[![Build status](https://ci.appveyor.com/api/projects/status/maf8tidwq1xbvrqh/branch/master?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-ravendb/branch/master) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.RavenDB.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.RavenDB/)

A [Serilog](https://serilog.net) sink that writes events as documents to [RavenDB](http://ravendb.net).

**Package** - [Serilog.Sinks.RavenDB](http://nuget.org/packages/serilog.sinks.ravendb)
| **Platforms** - .NET Standard 2.0 (.NET 4.6.1 or later; .NET Core 2.0 or later)

```csharp
var logs = new DocumentStore { ConnectionStringName = "Logs" }.Initialize();

var log = new LoggerConfiguration()
    .WriteTo.RavenDB(logs)
    .CreateLogger();
```

You'll need to create a database on the server for logs, and specify this as your default database in the connection string or `DocumentStore.DefaultDatabase`.  In the alternative, you can pass a default database when configuring the RavenDB sink. [More information](http://nblumhardt.com/2013/06/serilog-and-ravendb/).

You can also configure the sink through your application config file using [Serilog.Settings.AppSettings](https://www.nuget.org/packages/Serilog.Settings.AppSettings)
```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```
```xml
<connectionStrings>
  <add name="Logs" connectionString="Url=http://[RAVEN_DB_SERVER]:8080/;DefaultDatabase=[OPTIONAL_DEFAULT_DATABASE]" />
</connectionStrings>
<appSettings>
  <add key="serilog:minimum-level" value="Information" />
  <add key="serilog:using:RavenDB" value="Serilog.Sinks.RavenDB" />
  <add key="serilog:write-to:RavenDB.connectionStringName" value="Logs" />
  <add key="serilog:write-to:RavenDB.defaultDatabase" value="[DEFAULT_DATABASE]" />
</appSettings>
```

### Automatic Log Record Expiration

If you install the RavenDB expiration bundle on the database where log records are stored, you can configure the
sink to automatically delete log records by passing `errorExpiration` (for fatal and error messages) and
`expiration` (for all other messages). If you pass one, you should pass both. `Timeout.InfiniteTimeSpan` indicates that
messages of the appropriate type will never be deleted by the expiration bundle.
