using ApiPort;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Testing
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello World!");
                string reportPath = await new Analyzer().Analyze(@"C:\Users\Rahul\source\repos\Migra");
                Console.WriteLine(reportPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}

