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

You'll need to create a database on the server for logs, and specify this as your default database in the connection string or `DocumentStore.DefaultDatabase`.  In the alternative, you can pass a default database when configuring the RavenDB sink.

## Support for Automatic Log Record Expiration
If you install the RavenDB expiration bundle on the database where log records are stored, you can configure the
sink to automatically delete log records by passing errorExpirationTimeSpan (for fatal and error messages) and
expirationTimeSpan (for all other messages). If you pass one, you should pass both. TimeSpan.Zero indicates that
messages of the appropriate type will never be deleted by the expiration bundle.


[(More information.)](http://nblumhardt.com/2013/06/serilog-and-ravendb/)
