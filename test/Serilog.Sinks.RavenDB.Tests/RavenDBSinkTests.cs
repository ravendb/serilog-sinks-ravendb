using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Raven.Client;
using Raven.Client.ServerWide.Operations;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using LogEvent = Serilog.Sinks.RavenDB.Data.LogEvent;

namespace Serilog.Sinks.RavenDB.Tests
{
    public class RavenDBSinkTests
    {
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(50);

        static RavenDBSinkTests()
        {
            Raven.Embedded.EmbeddedServer.Instance.StartServer();
        }

        [Fact]
        public void WhenAnEventIsWrittenToTheSinkItIsRetrievableFromTheDocumentStore()
        {
            const string databaseName = nameof(WhenAnEventIsWrittenToTheSinkItIsRetrievableFromTheDocumentStore);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var events = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).ToList();
                    Assert.Single(events);
                    var single = events.Single();
                    Assert.Equal(messageTemplate, single.MessageTemplate);
                    Assert.Equal("\"New Macabre\"++", single.RenderedMessage);
                    Assert.Equal(timestamp, single.Timestamp);
                    Assert.Equal(level, single.Level);
                    Assert.Equal(1, single.Properties.Count);
                    Assert.Equal("New Macabre", single.Properties["Song"]);
                    Assert.Equal(exception.Message, single.Exception.Message);
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenAnEventIsWrittenWithExpirationItHasProperMetadata()
        {
            const string databaseName = nameof(WhenAnEventIsWrittenWithExpirationItHasProperMetadata);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = TimeSpan.FromDays(1);
                var errorExpiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.True(actualExpiration >= targetExpiration, $"The document should expire on or after {targetExpiration} but expires {actualExpiration}");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenAnEventIsWrittenWithExpirationCallbackItHasProperMetadata()
        {
            const string databaseName = nameof(WhenAnErrorEventIsWrittenWithExpirationItHasProperMetadata);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = TimeSpan.FromDays(1);
                var errorExpiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                TimeSpan func(Events.LogEvent le) => le.Level == LogEventLevel.Information ? expiration : errorExpiration;
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, logExpirationCallback: func))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.True(actualExpiration >= targetExpiration, $"The document should expire on or after {targetExpiration} but expires {actualExpiration}");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenAnErrorEventIsWrittenWithExpirationItHasProperMetadata()
        {
            const string databaseName = nameof(WhenAnErrorEventIsWrittenWithExpirationItHasProperMetadata);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = TimeSpan.FromDays(1);
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Error;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.True(actualExpiration >= targetExpiration, $"The document should expire on or after {targetExpiration} but expires {actualExpiration}");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenAFatalEventIsWrittenWithExpirationItHasProperMetadata()
        {
            const string databaseName = nameof(WhenAFatalEventIsWrittenWithExpirationItHasProperMetadata);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = TimeSpan.FromDays(1);
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Fatal;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.True(actualExpiration >= targetExpiration, $"The document should expire on or after {targetExpiration} but expires {actualExpiration}");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenNoErrorExpirationSetBuExpirationSetUseExpirationForErrors()
        {
            const string databaseName = nameof(WhenNoErrorExpirationSetBuExpirationSetUseExpirationForErrors);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Fatal;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.True(actualExpiration >= targetExpiration, $"The document should expire on or after {targetExpiration} but expires {actualExpiration}");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenNoExpirationSetBuErrorExpirationSetUseErrorExpirationForMessages()
        {
            const string databaseName = nameof(WhenNoExpirationSetBuErrorExpirationSetUseErrorExpirationForMessages);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.True(actualExpiration >= targetExpiration, $"The document should expire on or after {targetExpiration} but expires {actualExpiration}");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenNoExpirationIsProvidedMessagesDontExpire()
        {
            const string databaseName = nameof(WhenNoExpirationIsProvidedMessagesDontExpire);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();
                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Error;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    Assert.False(session.Advanced.GetMetadataFor(logEvent).ContainsKey(Constants.Documents.Metadata.Expires), "No expiration set");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenExpirationSetToInfiniteMessagesDontExpire()
        {
            const string databaseName = nameof(WhenExpirationSetToInfiniteMessagesDontExpire);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = Timeout.InfiniteTimeSpan;
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    Assert.False(session.Advanced.GetMetadataFor(logEvent).ContainsKey(Constants.Documents.Metadata.Expires), "No expiration set");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenErrorExpirationSetToInfiniteErrorsDontExpire()
        {
            const string databaseName = nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire);
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = Timeout.InfiniteTimeSpan;
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Error;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    Assert.False(session.Advanced.GetMetadataFor(logEvent).ContainsKey(Constants.Documents.Metadata.Expires), "No expiration set");
                }

                documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }

        [Fact]
        public void WhenUsingConnectionStringInCtorInternalDocumentStoreIsCreated()
        {
            var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
            var exception = new ArgumentException("Mládek");
            const LogEventLevel level = LogEventLevel.Information;
            const string messageTemplate = "{Song}++";
            var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };
            var events = new Dictionary<string, LogEvent>();

            const string databaseName = nameof(WhenUsingConnectionStringInCtorInternalDocumentStoreIsCreated);
            using (var store = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(databaseName))
            {
                store.OnBeforeStore += (sender, e) => events[e.DocumentId] = (LogEvent)e.Entity;
                store.Initialize();

                using (var ravenSink = new RavenDBSink(store, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                Assert.Single(events);
                var single = events.First().Value;
                Assert.Equal(messageTemplate, single.MessageTemplate);
                Assert.Equal("\"New Macabre\"++", single.RenderedMessage);
                Assert.Equal(timestamp, single.Timestamp);
                Assert.Equal(level, single.Level);
                Assert.Equal(1, single.Properties.Count);
                Assert.Equal("New Macabre", single.Properties["Song"]);
                Assert.Equal(exception.Message, single.Exception.Message);

                store.Maintenance.Server.Send(new DeleteDatabasesOperation(databaseName, hardDelete: true, fromNode: null, timeToWaitForConfirmation: null));
            }
        }
    }
}
