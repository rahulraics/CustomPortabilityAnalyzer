using ApiPort;
using Microsoft.Fx.Portability.ObjectModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Migrator
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string reportPath = await new Analyzer().Analyze(@"C:\Users\Rahul\source\repos\Migra");
            string sdfsd = ReadFile(reportPath);

            var obj = JsonConvert.DeserializeObject<RootObject>(ReadFile(reportPath));

            //Console.WriteLine(account.Email);
            Console.WriteLine(reportPath);
        }

        private static string ReadFile(string path)
        {
            return System.IO.File.ReadAllText(path);
        }
    }

    public class MissingDependency
    {
        public string DefinedInAssemblyIdentity { get; set; }
        public string MemberDocId { get; set; }
        public string TypeDocId { get; set; }
        public string RecommendedChanges { get; set; }
        public string SourceCompatibleChange { get; set; }
        public List<object> TargetStatus { get; set; }
    }

    public class RootObject
    {
        public string SubmissionId { get; set; }
        public string ApplicationName { get; set; }
        public DateTime CatalogLastUpdated { get; set; }
        public List<MissingDependency> MissingDependencies { get; set; }
        public List<string> UnresolvedUserAssemblies { get; set; }
        public List<string> Targets { get; set; }
        public List<object> BreakingChanges { get; set; }
        public List<object> BreakingChangeSkippedAssemblies { get; set; }
    }
}
