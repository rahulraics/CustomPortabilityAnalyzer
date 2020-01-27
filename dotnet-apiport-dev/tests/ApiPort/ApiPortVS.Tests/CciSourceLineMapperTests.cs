﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ApiPortVS.Contracts;
using ApiPortVS.Resources;
using ApiPortVS.SourceMapping;
using Microsoft.Fx.Portability;
using Microsoft.Fx.Portability.ObjectModel;
using Microsoft.Fx.Portability.Reporting;
using Microsoft.Fx.Portability.Reporting.ObjectModel;
using NSubstitute;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using Xunit;

namespace ApiPortVS.Tests
{
    public class CciSourceLineMapperTests
    {
        [Fact]
        public static void FindMissingTypeReferences_PdbNotFound_ReportedToTextOutput()
        {
            var assemblyName = "assemblyPath";
            var fileSystem = Substitute.For<IFileSystem>();
            fileSystem.FileExists(Arg.Any<string>()).ReturnsForAnyArgs(false);
            fileSystem.ChangeFileExtension(assemblyName, Arg.Any<string>()).Returns(assemblyName);

            var textOutput = Substitute.For<TextWriter>();
            var reporter = Substitute.For<IProgressReporter>();

            var traverser = new CciSourceLineMapper(fileSystem, textOutput, reporter);

            var analysis = Substitute.For<ReportingResult>(new List<FrameworkName>(), new List<MemberInfo>(), string.Empty, AnalyzeRequestFlags.None);
            traverser.GetSourceInfo(new string[] { assemblyName }, analysis);

            var expectedMessage = string.Format(CultureInfo.CurrentCulture, LocalizedStrings.PdbNotFoundFormat, assemblyName);
            reporter.Received(1).ReportIssue(expectedMessage);
        }
    }
}
