// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ApiPort.Resources;
using Microsoft.Fx.Portability;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ApiPort
{
    public class Analyzer
    {
        public async Task<string> Analyze(string path)
        {
            var productInformation = new ProductInformation("ApiPort_Console");
            string reportPath = string.Empty;

            string[] args = { "analyze", "-f", path };

            var options = CommandLineOptions.ParseCommandLineOptions(args);

            using (var container = DependencyBuilder.Build(options, productInformation))
            {
                var progressReport = container.Resolve<IProgressReporter>();

                try
                {
                    var client = container.Resolve<ConsoleApiPort>();

                    reportPath = await client.AnalyzeAssembliesAsync();

                    return reportPath;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return reportPath;
                }
            }
        }
    }
}