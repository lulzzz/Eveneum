﻿using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TechTalk.SpecFlow;
using Eveneum.Tests.Infrastrature;
using Eveneum.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Eveneum.Tests
{
    [Binding]
    public class WritingToStreamSteps
    {
        private readonly CosmosDbContext Context;

        WritingToStreamSteps(CosmosDbContext context)
        {
            this.Context = context;
        }

        [When(@"I write a new stream with (.*) events")]
        public async Task WhenIWriteNewStreamWithEvents(int events)
        {
            ScenarioContext.Current.SetStreamId(Guid.NewGuid().ToString());
            ScenarioContext.Current.SetNewEvents(TestSetup.GetEvents(events));
            ScenarioContext.Current.SetExistingDocuments(await CosmosSetup.QueryAllDocuments(this.Context.Client, this.Context.Database, this.Context.Collection));

            await this.Context.EventStore.WriteToStream(ScenarioContext.Current.GetStreamId(), ScenarioContext.Current.GetNewEvents(), metadata: ScenarioContext.Current.GetHeaderMetadata());
        }

        [When(@"I write a new stream with metadata and (.*) events")]
        public async Task WhenIWriteNewStreamWithMetadataAndNoEvents(int events)
        {
            ScenarioContext.Current.SetHeaderMetadata(TestSetup.GetMetadata());

            await WhenIWriteNewStreamWithEvents(events);
        }

        [Then(@"the header version (.*) with no metadata is persisted")]
        public async Task ThenTheHeaderVersionWithNoMetadataIsPersisted(ulong version)
        {
            var headerDocumentResponse = await this.Context.Client.ReadDocumentAsync<HeaderDocument>(UriFactory.CreateDocumentUri(this.Context.Database, this.Context.Collection, ScenarioContext.Current.GetStreamId()), new RequestOptions { PartitionKey = this.Context.PartitionKey });

            Assert.IsNotNull(headerDocumentResponse.Document);

            var headerDocument = headerDocumentResponse.Document;

            Assert.AreEqual(this.Context.Partition, headerDocument.Partition);
            Assert.AreEqual(DocumentType.Header, headerDocument.DocumentType);
            Assert.AreEqual(ScenarioContext.Current.GetStreamId(), headerDocument.StreamId);
            Assert.AreEqual(version, headerDocument.Version);
            Assert.AreEqual(version + EveneumDocument.GetOrderingFraction(DocumentType.Header), headerDocument.SortOrder);
            Assert.IsNull(headerDocument.MetadataType);
            Assert.IsFalse(headerDocument.Metadata.HasValues);
            Assert.NotNull(headerDocument.ETag);
            Assert.False(headerDocument.Deleted);
        }

        [Then(@"the header version (.*) with metadata is persisted")]
        public async Task ThenTheHeaderVersionWithMetadataIsPersisted(ulong version)
        {
            var headerDocumentResponse = await this.Context.Client.ReadDocumentAsync<HeaderDocument>(UriFactory.CreateDocumentUri(this.Context.Database, this.Context.Collection, ScenarioContext.Current.GetStreamId()), new RequestOptions { PartitionKey = this.Context.PartitionKey });

            Assert.IsNotNull(headerDocumentResponse.Document);

            var headerDocument = headerDocumentResponse.Document;

            Assert.AreEqual(this.Context.Partition, headerDocument.Partition);
            Assert.AreEqual(DocumentType.Header, headerDocument.DocumentType);
            Assert.AreEqual(ScenarioContext.Current.GetStreamId(), headerDocument.StreamId);
            Assert.AreEqual(version, headerDocument.Version);
            Assert.AreEqual(version + EveneumDocument.GetOrderingFraction(DocumentType.Header), headerDocument.SortOrder);
            Assert.AreEqual(typeof(SampleMetadata).AssemblyQualifiedName, headerDocument.MetadataType);
            Assert.NotNull(headerDocument.Metadata);
            Assert.AreEqual(JToken.FromObject(ScenarioContext.Current.GetHeaderMetadata()), headerDocument.Metadata);
            Assert.NotNull(headerDocument.ETag);
            Assert.False(headerDocument.Deleted);
        }

        [Then(@"no events are appended")]
        public async Task ThenNoEventsAreAppended()
        {
            var streamId = ScenarioContext.Current.GetStreamId();
            var currentDocuments = await CosmosSetup.QueryAllDocuments(this.Context.Client, this.Context.Database, this.Context.Collection);
            var existingDocumentIds = ScenarioContext.Current.GetExistingDocuments().Select(x => x.Id);

            var newEventDocuments = currentDocuments
                .OfType<EventDocument>()
                .Where(x => x.Partition == this.Context.Partition && x.StreamId == streamId)
                .Where(x => !existingDocumentIds.Contains(x.Id));

            Assert.IsEmpty(newEventDocuments);
        }

        [Then(@"new events are appended")]
        public async Task ThenNewEventsAreAppended()
        {
            var streamId = ScenarioContext.Current.GetStreamId();
            var newEvents = ScenarioContext.Current.GetNewEvents();
            var currentDocuments = await CosmosSetup.QueryAllDocuments(this.Context.Client, this.Context.Database, this.Context.Collection);

            foreach(var newEvent in newEvents)
            {
                var eventDocument = currentDocuments.OfType<EventDocument>().SingleOrDefault(x => x.Partition == this.Context.Partition && x.Id == EventDocument.GenerateId(streamId, newEvent.Version));

                Assert.IsNotNull(eventDocument);
                Assert.AreEqual(DocumentType.Event, eventDocument.DocumentType);
                Assert.AreEqual(streamId, eventDocument.StreamId);
                Assert.AreEqual(newEvent.Body.GetType().AssemblyQualifiedName, eventDocument.BodyType);
                Assert.NotNull(eventDocument.Body);
                Assert.AreEqual(JToken.FromObject(newEvent.Body), eventDocument.Body);
                Assert.NotNull(eventDocument.ETag);
                Assert.False(eventDocument.Deleted);
            }
        }
    }
}