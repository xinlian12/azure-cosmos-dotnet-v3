﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract class ReadManyHelper
    {
        public abstract Task<ResponseMessage> ExecuteReadManyRequestAsync(IReadOnlyList<(string, PartitionKey)> items,
                                                                        ITrace trace);

        public abstract Task<FeedResponse<T>> ExecuteReadManyRequestAsync<T>(IReadOnlyList<(string, PartitionKey)> items,
                                                                            ITrace trace);
    }
}
