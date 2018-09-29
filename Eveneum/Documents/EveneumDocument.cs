﻿using System;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Eveneum.Documents
{
    enum DocumentType { Header = 1, Event, Snapshot }

    abstract class EveneumDocument
    {
        public const char Separator = '~';

        public string Partition { get; set; }

        [JsonProperty(PropertyName = "id")]
        public virtual string Id { get; }

        [JsonProperty(PropertyName = "_etag")]
        public string ETag { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public abstract DocumentType DocumentType { get; }

        public string StreamId { get; set; }
        public ulong Version { get; set; }

        public string Type { get; set; }
        public JObject Body { get; set; }
        public bool Deleted { get; set; }

        public decimal SortOrder => this.Version + GetOrderingFraction(this.DocumentType);

        internal static decimal GetOrderingFraction(DocumentType documentType)
        {
            switch(documentType)
            {
                case DocumentType.Header:
                    return 0.3M;
                case DocumentType.Snapshot:
                    return 0.2M;
                case DocumentType.Event:
                    return 0.1M;
                default:
                    throw new NotSupportedException($"Document type '{documentType}' is not supported.");
            }
        }

        public static EveneumDocument Parse(Document document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var documentType = document.GetPropertyValue<DocumentType>(nameof(DocumentType));

            switch (documentType)
            {
                case DocumentType.Header:
                    return JsonConvert.DeserializeObject<HeaderDocument>(document.ToString());

                case DocumentType.Snapshot:
                    return JsonConvert.DeserializeObject<SnapshotDocument>(document.ToString());
                    
                case DocumentType.Event:
                    return JsonConvert.DeserializeObject<EventDocument>(document.ToString());

                default:
                    throw new NotSupportedException($"Cannot parse document of type '{document.GetType().AssemblyQualifiedName}' with DocumentType '{document}'.");
            }
        }
    }
}
