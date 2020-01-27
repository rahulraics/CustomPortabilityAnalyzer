﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Fx.Portability.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Xunit;

namespace Microsoft.Fx.Portability.Tests
{
    public class SerializationTests
    {
        [Fact]
        public static void SerializeAnalyzeRequest()
        {
            var request = new AnalyzeRequest
            {
                ApplicationName = "name",
                Dependencies = GetDependencies(),
                Targets = new List<string> { "target1", "target2" },
                UnresolvedAssemblies = new List<string> { "assembly1", "assembly2" },
                UserAssemblies = new List<AssemblyInfo> { new AssemblyInfo { AssemblyIdentity = "name1" }, new AssemblyInfo { AssemblyIdentity = "name2" } },
                Version = AnalyzeRequest.CurrentVersion
            };

            var newtonsoft = request.Serialize().Deserialize<AnalyzeRequest>();

            CompareAnalyzeRequest(request, newtonsoft);
        }

        [Fact]
        public static void SerializeAnalyzeResponse()
        {
            var response = new AnalyzeResponse
            {
                MissingDependencies = new List<MemberInfo> { new MemberInfo { MemberDocId = "doc1" }, new MemberInfo { MemberDocId = "doc2" } },
                SubmissionId = Guid.NewGuid().ToString(),
                Targets = new List<FrameworkName> { new FrameworkName("target1", Version.Parse("1.0.0.0")) },
                UnresolvedUserAssemblies = new List<string> { "assembly1", "assembly2", "assembly3" }
            };

            var newtonsoft = response.Serialize().Deserialize<AnalyzeResponse>();

            CompareAnalyzeResponse(response, newtonsoft);
        }

        [Fact]
        public static void SerializeProjectSubmission()
        {
            var submission1 = new ProjectSubmission { Name = "test1", Length = 10, SubmittedDate = new DateTime(10, 1, 4) };
            var submission2 = new ProjectSubmission { Name = "test2", Length = 11, SubmittedDate = new DateTime(10, 2, 4) };
            var submission3 = new ProjectSubmission { Name = "test3", Length = 12, SubmittedDate = new DateTime(10, 3, 6) };

            var list = new List<ProjectSubmission> { submission1, submission2, submission3 };

            var serialized = list.Serialize();
            var deserialized = serialized.Deserialize<IEnumerable<ProjectSubmission>>();

            CollectionAssertAreEquivalent(list, deserialized);
        }

        [Fact]
        public static void TestFrameworkNames()
        {
            VerifySerialization(new FrameworkName("name", new Version("1.2.3.4")));
            VerifySerialization(new FrameworkName("name", new Version("1.2.3.0")));
            VerifySerialization(new FrameworkName("name", new Version("1.2.0.0")));
            VerifySerialization(new FrameworkName("name", new Version("1.2.0.4")));
            VerifySerialization(new FrameworkName("name", new Version("1.2.1")));
            VerifySerialization(new FrameworkName("name", new Version("1.0")));
        }

        [Fact]
        public static void TestVersion()
        {
            VerifySerialization(new Version("1.2.3.4"));
            VerifySerialization(new Version("1.2.3.0"));
            VerifySerialization(new Version("1.2.0.0"));
            VerifySerialization(new Version("1.2.0.4"));
            VerifySerialization(new Version("1.2.1"));
            VerifySerialization(new Version("1.0"));
        }

        [Fact]
        public static void TestEmptyValues()
        {
            VerifyEmptySerialized<AnalyzeRequest>();
            VerifyEmptySerialized<FrameworkName>();
            VerifyEmptySerialized<Version>();
        }

        private static IDictionary<MemberInfo, ICollection<AssemblyInfo>> GetDependencies()
        {
            var dict = new Dictionary<MemberInfo, ICollection<AssemblyInfo>>
            {
                { new MemberInfo { MemberDocId = "item1" }, new HashSet<AssemblyInfo> { new AssemblyInfo { AssemblyIdentity = "string1" }, new AssemblyInfo { AssemblyIdentity = "string2" } } },
                { new MemberInfo { MemberDocId = "item2" }, new HashSet<AssemblyInfo> { new AssemblyInfo { AssemblyIdentity = "string3" }, new AssemblyInfo { AssemblyIdentity = "string4" } } },
                { new MemberInfo { MemberDocId = "item3" }, new HashSet<AssemblyInfo> { new AssemblyInfo { AssemblyIdentity = "string5" }, new AssemblyInfo { AssemblyIdentity = "string6" } } }
            };

            return dict;
        }

        private static void CollectionAssertAreEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            Assert.Equal<IEnumerable<T>>(expected.ToList().OrderBy(k => k), actual.ToList().OrderBy(k => k));
        }

        private static void VerifyEmptySerialized<T>()
            where T : class
        {
            var deserialized = string.Empty.Serialize().Deserialize<T>();

            Assert.Null(deserialized);
        }

        private static void VerifySerialization<T>(T o)
        {
            var deserialized = o.Serialize().Deserialize<T>();

            Assert.Equal(o, deserialized);
        }

        private static void CompareAnalyzeRequest(AnalyzeRequest request, AnalyzeRequest deserialized)
        {
            Assert.Equal(request.ApplicationName, deserialized.ApplicationName);

            // Verify dependencies
            CollectionAssertAreEquivalent(request.Dependencies.Keys, deserialized.Dependencies.Keys);

            foreach (var item in request.Dependencies.Keys)
            {
                CollectionAssertAreEquivalent(request.Dependencies[item], deserialized.Dependencies[item]);
            }

            Assert.Equal<List<string>>(request.Targets.OrderBy(k => k).ToList(), deserialized.Targets.OrderBy(k => k).ToList());
            Assert.Equal<List<string>>(request.UnresolvedAssemblies.OrderBy(k => k).ToList(), deserialized.UnresolvedAssemblies.OrderBy(k => k).ToList());
            Assert.Equal<List<AssemblyInfo>>(request.UserAssemblies.OrderBy(k => k.AssemblyIdentity).ToList(), deserialized.UserAssemblies.OrderBy(k => k.AssemblyIdentity).ToList());
            Assert.Equal(request.Version, deserialized.Version);
        }

        private static void CompareAnalyzeResponse(AnalyzeResponse response, AnalyzeResponse deserialized)
        {
            CollectionAssertAreEquivalent(response.MissingDependencies, deserialized.MissingDependencies);
            Assert.Equal(response.SubmissionId, deserialized.SubmissionId);
            CollectionAssertAreEquivalent(response.Targets, deserialized.Targets);
            CollectionAssertAreEquivalent(response.UnresolvedUserAssemblies, deserialized.UnresolvedUserAssemblies);
        }
    }
}
