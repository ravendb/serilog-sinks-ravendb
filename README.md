# Serilog.Sinks.RavenDB

[![Build status](https://ci.appveyor.com/api/projects/status/maf8tidwq1xbvrqh/branch/master?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-ravendb/branch/master)

A Serilog sink that writes events as documents to [RavenDB](http://ravendb.net).

**Package** - [Serilog.Sinks.RavenDB](http://nuget.org/packages/serilog.sinks.ravendb)
| **Platforms** - .NET 4.5

```csharp
var logs = new DocumentStore { ConnectionStringName = "Logs" }.Initialize();

var log = new LoggerConfiguration()
    .WriteTo.RavenDB(logs)
    .CreateLogger();
```

You'll need to create a database on the server for logs, and specify this as your default database in the connection string or `DocumentStore.DefaultDatabase`.

[(More information.)](http://nblumhardt.com/2013/06/serilog-and-ravendb/)
