﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Fx.Portability.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Xunit;

namespace Microsoft.Fx.Portability.Tests.ObjectModel
{
    public class NuGetPackageInfoTests
    {
        [Fact]
        public static void NuGetPackageInfoCreated()
        {
            var assemblyInfo = "MyDll, Version=1.5.3";
            var frameworkName = new FrameworkName("SomeFramework", Version.Parse("5.6.7.2"));
            var package1 = "MyNuGetPackageId.1";
            var package1Version = "1.2.2";

            var packageInfo = new NuGetPackageInfo(package1, new Dictionary<FrameworkName, string> { { frameworkName, package1Version } }, assemblyInfo);

            Assert.Equal(package1, packageInfo.PackageId);
            Assert.Equal(assemblyInfo, packageInfo.AssemblyInfo);
            Assert.Equal(frameworkName, packageInfo.SupportedVersions.First().Key);
        }

        [Fact]
        public static void Equality()
        {
            // Set up
            var assemblyName = "MyNuGetPackage, Version=1.0.4";
            var frameworkName = new FrameworkName("SomeFramework", Version.Parse("5.6.7.2"));
            var package1 = "MyNuGetPackageId.1";
            var package1Version1 = "1.2.2";
            var package1Version2 = "1.2.3";

            var original = new NuGetPackageInfo(package1, new Dictionary<FrameworkName, string> { { frameworkName, package1Version1 } }, assemblyName);
            var compared = new NuGetPackageInfo(package1, new Dictionary<FrameworkName, string> { { frameworkName, package1Version1 } }, assemblyName);
            var comparedNotSamePackages = new NuGetPackageInfo(package1, new Dictionary<FrameworkName, string> { { frameworkName, package1Version2 } }, assemblyName);

            // Act & Assert
            Assert.True(original.Equals(compared));

            Assert.False(original.Equals("something"));
            Assert.False(original.Equals(null));
            Assert.False(original.Equals(comparedNotSamePackages));
        }

        [Fact]
        public static void InvalidConstruction()
        {
            var assemblyInfo = "MyDll, Version=1.5.3";
            var frameworkName = new FrameworkName("SomeFramework", Version.Parse("5.6.7.2"));
            var package1 = "MyNuGetPackageId.1";
            var package1Version = "1.2.2";

            var packageInfo = new NuGetPackageInfo(package1, new Dictionary<FrameworkName, string> { { frameworkName, package1Version } }, assemblyInfo);

            Assert.Throws<ArgumentNullException>(() => new NuGetPackageInfo(null, new Dictionary<FrameworkName, string> { { frameworkName, package1Version } }, assemblyInfo));
            Assert.Throws<ArgumentException>(() => new NuGetPackageInfo(string.Empty, new Dictionary<FrameworkName, string> { { frameworkName, package1Version } }, assemblyInfo));
            Assert.Throws<ArgumentNullException>(() => new NuGetPackageInfo(package1, new Dictionary<FrameworkName, string> { { null, package1Version } }, assemblyInfo));
        }
    }
}
