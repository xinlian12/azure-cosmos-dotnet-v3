﻿namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryPartitionRangePageAsyncEnumeratorTests
    {
        [TestMethod]
        public async Task Test429sAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sAsync();
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sWithContinuationsAsync();
        }

        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestDrainFullyAsync();
        }

        [TestMethod]
        public async Task TestEmptyPages()
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages();
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestResumingFromStateAsync();
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitAsync();
        }

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<QueryPage, QueryState>
        {
            public Implementation()
                : base(singlePartition: true)
            {
            }

            [TestMethod]
            public async Task TestSplitAsync()
            {
                int numItems = 100;
                DocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<QueryPage>> enumerator = this.CreateEnumerator(documentContainer);

                (HashSet<string> parentIdentifiers, QueryState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue(enumerator.Current.Failed);

                // Resume on the children using the parent continuaiton token
                HashSet<string> childIdentifiers = new HashSet<string>();
                foreach (int partitionKeyRangeId in new int[] { 1, 2 })
                {
                    IAsyncEnumerable<TryCatch<QueryPage>> enumerable = new PartitionRangePageAsyncEnumerable<QueryPage, QueryState>(
                        range: new PartitionKeyRange()
                        {
                            Id = partitionKeyRangeId.ToString(),
                            MinInclusive = partitionKeyRangeId.ToString(),
                            MaxExclusive = partitionKeyRangeId.ToString(),
                        },
                        state: state,
                        (range, state) => new QueryPartitionRangePageAsyncEnumerator(
                            queryDataSource: documentContainer,
                            sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                            feedRange: range,
                            pageSize: 10,
                            state: state));
                    HashSet<string> resourceIdentifiers = await this.DrainFullyAsync(enumerable);

                    childIdentifiers.UnionWith(resourceIdentifiers);
                }

                Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(QueryPage page)
            {
                List<Record> records = new List<Record>(page.Documents.Count);
                foreach (CosmosElement element in page.Documents)
                {
                    CosmosObject document = (CosmosObject)element;
                    long resourceIdentifier = Number64.ToLong(((CosmosNumber)document["_rid"]).Value);
                    long timestamp = Number64.ToLong(((CosmosNumber)document["_ts"]).Value);
                    string identifer = ((CosmosString)document["id"]).Value;

                    records.Add(new Record(resourceIdentifier, timestamp, identifer, document));
                }

                return records;
            }

            public override IAsyncEnumerable<TryCatch<QueryPage>> CreateEnumerable(
                DocumentContainer documentContainer,
                QueryState state = null)
            {
                return new PartitionRangePageAsyncEnumerable<QueryPage, QueryState>(
                    range: new PartitionKeyRange()
                    {
                        Id = "0",
                        MinInclusive = "0",
                        MaxExclusive = "0",
                    },
                    state: state,
                    (range, state) => new QueryPartitionRangePageAsyncEnumerator(
                        queryDataSource: documentContainer,
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRange: range,
                        pageSize: 10,
                        state: state));
            }

            public override IAsyncEnumerator<TryCatch<QueryPage>> CreateEnumerator(
                DocumentContainer documentContainer,
                QueryState state = default)
            {
                return new QueryPartitionRangePageAsyncEnumerator(
                    queryDataSource: documentContainer,
                    sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                    feedRange: new PartitionKeyRange()
                    {
                        Id = "0",
                        MinInclusive = "0",
                        MaxExclusive = "0",
                    },
                    pageSize: 10,
                    state: state);
            }
        }
    }
}
