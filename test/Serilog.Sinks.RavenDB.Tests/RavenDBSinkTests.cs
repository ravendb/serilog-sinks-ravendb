using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Raven.Client.Documents;
using Serilog.Events;
using Serilog.Parsing;
using LogEvent = Serilog.Sinks.RavenDB.Data.LogEvent;

namespace Serilog.Sinks.RavenDB.Tests
{
    [TestFixture]
    public class RavenDBSinkTests
    {
        static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(50);

        [OneTimeSetUp]
        public void Setup()
        {
            Raven.Embedded.EmbeddedServer.Instance.StartServer();
        }
        
        [Test]
        public void WhenAnEventIsWrittenToTheSinkItIsRetrievableFromTheDocumentStore()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenAnEventIsWrittenToTheSinkItIsRetrievableFromTheDocumentStore)))
            {
                documentStore.Initialize();
                
                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
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
                    Assert.GreaterOrEqual(events.Count, 1);

                    var single = events.First(f => f.Timestamp == timestamp);
                    Assert.AreEqual(messageTemplate, single.MessageTemplate);
                    Assert.AreEqual("\"New Macabre\"++", single.RenderedMessage);
                    Assert.AreEqual(timestamp, single.Timestamp);
                    Assert.AreEqual(level, single.Level);
                    Assert.AreEqual(1, single.Properties.Count);
                    Assert.AreEqual("New Macabre", single.Properties["Song"]);
                    Assert.AreEqual(exception.Message, single.Exception.Message);
                }
            }
        }

        [Test]
        public void WnenAnEventIsWrittenWithExpirationItHasProperMetadata()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
                var expiration = TimeSpan.FromDays(1);
                var errorExpiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration:expiration, errorExpiration:errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                { 
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[RavenDBSink.RavenExpirationDate].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WnenAnErrorEventIsWrittenWithExpirationItHasProperMetadata()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
                var errorExpiration = TimeSpan.FromDays(1);
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Error;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration:errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[RavenDBSink.RavenExpirationDate].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenAFatalEventIsWrittenWithExpirationItHasProperMetadata()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
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
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[RavenDBSink.RavenExpirationDate].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenNoErrorExpirationSetBuExpirationSetUseExpirationForErrors()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
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
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[RavenDBSink.RavenExpirationDate].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenNoExpirationSetBuErrorExpirationSetUseErrorExpirationForMessages()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
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
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[RavenDBSink.RavenExpirationDate].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenNoExpirationIsProvidedMessagesDontExpire()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();
                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
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
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    Assert.IsFalse(session.Advanced.GetMetadataFor(logEvent).ContainsKey(RavenDBSink.RavenExpirationDate), "No expiration set");
                }
            }
        }

        [Test]
        public void WhenExpirationSetToInfiniteMessagesDontExpire()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
                var expiration = Timeout.InfiniteTimeSpan;
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Mládek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration:expiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var events = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).As<LogEvent>().ToList();

                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    Assert.IsFalse(session.Advanced.GetMetadataFor(logEvent).ContainsKey(RavenDBSink.RavenExpirationDate), "No expiration set");
                }
            }
        }

        [Test]
        public void WhenErrorExpirationSetToInfiniteErrorsDontExpire()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenErrorExpirationSetToInfiniteErrorsDontExpire)))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
                var errorExpiration = Timeout.InfiniteTimeSpan;
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
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).OrderByDescending(e => e.Timestamp).First();
                    Assert.IsFalse(session.Advanced.GetMetadataFor(logEvent).ContainsKey(RavenDBSink.RavenExpirationDate), "No expiration set");
                }
            }
        }

        [Test]
        public void WhenUsingConnectionStringInCtorInternalDocumentStoreIsCreated()
        {
            var timestamp = new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromHours(0));
            var exception = new ArgumentException("Mládek");
            const LogEventLevel level = LogEventLevel.Information;
            const string messageTemplate = "{Song}++";
            var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };
            var events = new Dictionary<string, LogEvent>();

            using (var store = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(nameof(WhenUsingConnectionStringInCtorInternalDocumentStoreIsCreated)))
            {
                store.OnBeforeStore += (sender, e) => events[e.DocumentId] = (LogEvent)e.Entity;
                store.Initialize();

                using (var ravenSink = new RavenDBSink(store, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                Assert.AreEqual(1, events.Count);
                var single = events.Values.OrderByDescending(e => e.Timestamp).First();
                Assert.AreEqual(messageTemplate, single.MessageTemplate);
                Assert.AreEqual("\"New Macabre\"++", single.RenderedMessage);
                Assert.AreEqual(timestamp, single.Timestamp);
                Assert.AreEqual(level, single.Level);
                Assert.AreEqual(1, single.Properties.Count);
                Assert.AreEqual("New Macabre", single.Properties["Song"]);
                Assert.AreEqual(exception.Message, single.Exception.Message);
            }
        }

    }
}
