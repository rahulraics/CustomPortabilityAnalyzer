﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ApiPortVS
{
    internal static class Guids
    {
        public const string ApiPortVSPkgString = "05b3d9f1-6699-4f10-a9e4-da2ed1248523";
        private const string AnalyzeMenuItemCmdSetString = "d4c08529-d7a0-4ee1-8093-fb5d4b54e36c";
        private const string ProjectContextMenuItemCmdSetString = "5AD32A4E-F8D4-4675-9914-CD514C32FF6D";
        private const string SolutionContextMenuItemCmdSetString = "1A55772C-AEA4-41D1-99EF-0A8278AED5AE";

        public static readonly Guid AnalyzeMenuItemCmdSet = new Guid(AnalyzeMenuItemCmdSetString);
        public static readonly Guid ProjectContextMenuItemCmdSet = new Guid(ProjectContextMenuItemCmdSetString);
        public static readonly Guid SolutionContextMenuItemCmdSet = new Guid(SolutionContextMenuItemCmdSetString);
    }
}
