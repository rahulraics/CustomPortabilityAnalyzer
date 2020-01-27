using ApiPort;
using System;
using System.Threading.Tasks;

namespace asdasdasd
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string reportPath = await new Analyzer().Analyze(@"C:\Users\Rahul\source\repos\Migra");
            Console.WriteLine(reportPath);

        }
    }
}
