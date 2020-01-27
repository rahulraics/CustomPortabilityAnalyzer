﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Fx.Portability.Analyzer;
using Microsoft.Fx.Portability.ObjectModel;
using Microsoft.Fx.Portability.Reporting;
using Microsoft.Fx.Portability.Reports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Fx.Portability
{
    public class OfflineApiPortService : IApiPortService
    {
        private readonly ITargetMapper _mapper;
        private readonly IApiCatalogLookup _lookup;
        private readonly ICollection<FrameworkName> _defaultTargets;
        private readonly IRequestAnalyzer _requestAnalyzer;
        private readonly ICollection<IReportWriter> _reportWriters;
        private readonly ISearcher<string> _searcher;
        private readonly IApiRecommendations _apiRecommendations;

        public OfflineApiPortService(IApiCatalogLookup lookup, IRequestAnalyzer requestAnalyzer, ITargetMapper mapper, ICollection<IReportWriter> reportWriters, ITargetNameParser targetNameParser, IApiRecommendations apiRecommendations)
        {
            _lookup = lookup;
            _requestAnalyzer = requestAnalyzer;
            _mapper = mapper;
            _reportWriters = reportWriters;
            _defaultTargets = new HashSet<FrameworkName>(targetNameParser.DefaultTargets);
            _searcher = new StringContainsSearcher(lookup);
            _apiRecommendations = apiRecommendations;
        }

        public Task<ServiceResponse<IEnumerable<AvailableTarget>>> GetTargetsAsync()
        {
            var targets = _lookup
                .GetPublicTargets()
                .Select(target => new AvailableTarget { Name = target.Identifier, Version = target.Version, IsSet = _defaultTargets.Contains(target) })
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);

            var response = new ServiceResponse<IEnumerable<AvailableTarget>>(targets);

            return Task.FromResult(response);
        }

        public Task<ServiceResponse<AnalyzeResponse>> SendAnalysisAsync(AnalyzeRequest a)
        {
            var response = _requestAnalyzer.AnalyzeRequest(a, Guid.NewGuid().ToString());
            var serviceResponse = new ServiceResponse<AnalyzeResponse>(response);

            return Task.FromResult(serviceResponse);
        }

        public async Task<ServiceResponse<IEnumerable<ReportingResultWithFormat>>> SendAnalysisAsync(AnalyzeRequest a, IEnumerable<string> formats)
        {
            var response = _requestAnalyzer.AnalyzeRequest(a, Guid.NewGuid().ToString());

            if (!formats?.Any() ?? true)
            {
                var defaultFormat = await GetDefaultResultFormatAsync();
                formats = new[] { defaultFormat.Response.DisplayName };
            }

            var formatSet = new HashSet<string>(formats, StringComparer.OrdinalIgnoreCase);

            var result = new List<ReportingResultWithFormat>();

            foreach (var writer in _reportWriters.Where(w => formatSet.Contains(w.Format.DisplayName)))
            {
                using (var ms = new MemoryStream())
                {
                    await writer.WriteStreamAsync(ms, response);

                    result.Add(new ReportingResultWithFormat
                    {
                        Format = writer.Format.DisplayName,
                        Data = ms.ToArray()
                    });
                }
            }

            return await WrapResponse<IEnumerable<ReportingResultWithFormat>>(result);
        }

        public Task<ServiceResponse<IEnumerable<ResultFormatInformation>>> GetResultFormatsAsync()
        {
            var formats = _reportWriters.Select(r => r.Format);

            return WrapResponse(formats);
        }

        public Task<ServiceResponse<ResultFormatInformation>> GetDefaultResultFormatAsync()
        {
            var format = new JsonReportWriter().Format;
            return WrapResponse(format);
        }

        private static Task<ServiceResponse<T>> WrapResponse<T>(T data)
        {
            var response = new ServiceResponse<T>(data);

            return Task.FromResult(response);
        }

        public static Task<ServiceResponse<AnalyzeResponse>> GetAnalysisAsync(string submissionId)
        {
            throw new NotImplementedException();
        }

        public static Task<ServiceResponse<byte[]>> GetAnalysisAsync(string submissionId, string format)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResponse<ApiInformation>> GetApiInformationAsync(string docId)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResponse<IReadOnlyCollection<ApiDefinition>>> SearchFxApiAsync(string query, int? top = null)
        {
            var queryResult = await _searcher.SearchAsync(query, top ?? 10);

            // TODO: This currently only populates the docid, as that is the only thing the offline service currently requires
            var result = queryResult.Select(r => new ApiDefinition { DocId = r })
                .ToList()
                .AsReadOnly();

            return new ServiceResponse<IReadOnlyCollection<ApiDefinition>>(result);
        }

        public Task<ServiceResponse<IReadOnlyCollection<ApiInformation>>> QueryDocIdsAsync(IEnumerable<string> docIds)
        {
            if (docIds == null)
            {
                throw new ArgumentNullException(nameof(docIds));
            }

            // return the ApiInformation for all valid Ids
            var result = docIds
                        .Distinct(StringComparer.Ordinal)
                        .Where(_lookup.IsFrameworkMember)
                        .Select(d => new ApiInformation(d, _lookup, _apiRecommendations))
                        .ToList()
                        .AsReadOnly();

            return Task.FromResult(new ServiceResponse<IReadOnlyCollection<ApiInformation>>(result));
        }

        private async Task<IEnumerable<ResultFormatInformation>> GetResultFormatAsync(IEnumerable<string> format)
        {
            var requestedFormats = new HashSet<string>(format, StringComparer.OrdinalIgnoreCase);
            var formats = await GetResultFormatsAsync();
            var formatInformation = formats.Response
                .Where(r => requestedFormats.Contains(r.DisplayName))
                .ToList();

            var unknownFormats = requestedFormats
                .Except(formatInformation.Select(f => f.DisplayName), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unknownFormats.Any())
            {
                throw new UnknownReportFormatException(unknownFormats);
            }

            return formatInformation;
        }
    }
}
