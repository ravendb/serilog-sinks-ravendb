// Copyright 2020 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Sinks.PeriodicBatching;
using LogEvent = Serilog.Sinks.RavenDB.Data.LogEvent;
using Serilog.Events;
using Raven.Client;
using Raven.Client.Documents;

namespace Serilog.Sinks.RavenDB
{
    /// <summary>
    /// Writes log events as documents to a RavenDB database.
    /// </summary>
    public class RavenDBSink : PeriodicBatchingSink
    {
        private readonly IFormatProvider _formatProvider;
        private readonly IDocumentStore _documentStore;
        private readonly string _defaultDatabase;
        private readonly TimeSpan? _expiration;
        private readonly TimeSpan? _errorExpiration;
        private readonly bool _disposeDocumentStore;

        /// <summary>
        /// A reasonable default for the number of events posted in
        /// each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 50;

        /// <summary>
        /// A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Construct a sink posting to the specified database.
        /// </summary>
        /// <param name="documentStore">A documentstore for a RavenDB database.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="defaultDatabase">Optional name of default database</param>
        /// <param name="expiration">Optional time before a logged message will be expired assuming the expiration bundle is installed. <see cref="System.Threading.Timeout.InfiniteTimeSpan">Timeout.InfiniteTimeSpan</see> (-00:00:00.0010000) means no expiration. If this is not provided but errorExpiration is, errorExpiration will be used for non-errors too.</param>
        /// <param name="errorExpiration">Optional time before a logged error message will be expired assuming the expiration bundle is installed. <see cref="System.Threading.Timeout.InfiniteTimeSpan">Timeout.InfiniteTimeSpan</see> (-00:00:00.0010000) means no expiration. If this is not provided but expiration is, expiration will be used for errors too.</param>
        public RavenDBSink(IDocumentStore documentStore, int batchPostingLimit, TimeSpan period, IFormatProvider formatProvider, string defaultDatabase = null, TimeSpan? expiration = null, TimeSpan? errorExpiration = null)
            : base(batchPostingLimit, period)
        {
            if (documentStore == null) throw new ArgumentNullException(nameof(documentStore));
            _formatProvider = formatProvider;
            _documentStore = documentStore;
            _defaultDatabase = defaultDatabase;
            _expiration = expiration;
            _errorExpiration = errorExpiration ?? expiration;
            _expiration = expiration ?? errorExpiration;
            _disposeDocumentStore = false;
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <remarks>Override either <see cref="PeriodicBatchingSink.EmitBatch"/> or <see cref="PeriodicBatchingSink.EmitBatchAsync"/>,
        /// not both.</remarks>
        protected override async Task EmitBatchAsync(IEnumerable<global::Serilog.Events.LogEvent> events)
        {
            using (var session = string.IsNullOrWhiteSpace(_defaultDatabase) ? _documentStore.OpenAsyncSession() : _documentStore.OpenAsyncSession(_defaultDatabase))
            {
                foreach (var logEvent in events)
                {
                    var logEventDoc = new LogEvent(logEvent, logEvent.RenderMessage(_formatProvider));
                    await session.StoreAsync(logEventDoc);
                    if (_expiration != null)
                    {
                        if (logEvent.Level == LogEventLevel.Error || logEvent.Level == LogEventLevel.Fatal)
                        {
                            if (_errorExpiration != Timeout.InfiniteTimeSpan)
                            {
                                var metaData = session.Advanced.GetMetadataFor(logEventDoc);
                                metaData[Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(_errorExpiration.Value);
                            }
                        }
                        else
                        {
                            if (_expiration != Timeout.InfiniteTimeSpan)
                            {
                                var metaData = session.Advanced.GetMetadataFor(logEventDoc);
                                metaData[Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(_expiration.Value);
                            }
                        }
                    }
                }
                await session.SaveChangesAsync();
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _disposeDocumentStore)
            {
                _documentStore.Dispose();
            }
        }
    }
}
