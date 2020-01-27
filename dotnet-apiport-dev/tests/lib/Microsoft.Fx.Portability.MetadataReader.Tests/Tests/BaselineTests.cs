﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Fx.Portability.Analyzer;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Fx.Portability.MetadataReader.Tests
{
    public class BaselineTests
    {
        public void MscorlibTest()
        {
            var dependencyFilter = Substitute.For<IDependencyFilter>();

            var mscorlib = typeof(object).GetTypeInfo().Assembly.Location;

            var baseline = GetBaseline(mscorlib);
            var dependencyFinder = new ReflectionMetadataDependencyFinder(dependencyFilter);
            var path = new AssemblyFileClass(new FileInfo(mscorlib));
            var assemblyFile = Substitute.For<IAssemblyFile>();
            var progressReporter = Substitute.For<IProgressReporter>();

            var dependencies = dependencyFinder.FindDependencies(new[] { path }, progressReporter);

            var result = dependencies.Dependencies
                .Select(d => d.Key)
                .OrderBy(d => d);

            Assert.Equal(baseline, result);
        }

        private IEnumerable<ObjectModel.MemberInfo> GetBaseline(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var version = FileVersionInfo.GetVersionInfo(path);
            var version_file = FormattableString.Invariant($"{fileName}_{version.ProductVersion}.json");
            var dataFile = FormattableString.Invariant($"Data.{version_file}");

            using (var data = typeof(ManagedMetadataReaderTests).GetTypeInfo().Assembly.GetManifestResourceStream(dataFile))
            {
                if (data == null)
                {
                    Assert.True(false, ((FormattableString)$"Could not find baseline file for {fileName} version={version.ProductVersion}").ToString(CultureInfo.CurrentCulture));
                }

                return data.Deserialize<IEnumerable<ObjectModel.MemberInfo>>();
            }
        }

        private class AssemblyFileClass : IAssemblyFile
        {
            private readonly FileInfo _file;
            public AssemblyFileClass(FileInfo info) => _file = info;

            public string Name => _file.Name;

            public string Version => FileVersionInfo.GetVersionInfo(_file.FullName).FileVersion;

            public bool Exists => _file.Exists;

            public Stream OpenRead() => _file.OpenRead();
        }

    }
}
