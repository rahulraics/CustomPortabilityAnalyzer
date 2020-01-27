﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Fx.Portability.ObjectModel
{
    public interface IStorage
    {
        Task<bool> SaveToBlobAsync(AnalyzeRequest analyzeRequest, string submissionId);

        Task<AnalyzeRequest> RetrieveRequestAsync(string uniqueId);

        Task<IEnumerable<string>> RetrieveSubmissionIdsAsync();

        Task AddJobToQueueAsync(string submissionId);

        IEnumerable<ProjectSubmission> GetProjectSubmissions();
    }
}
