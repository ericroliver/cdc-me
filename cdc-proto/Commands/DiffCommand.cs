using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc;

namespace Softbase
{
    public class DiffCommand : Command
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public DiffCommand(SimpleDac dac, ILoggerFactory factory)
           : base("diff", "Generate a diff between two profiles")
        {
            _dac = dac;
            _logger = factory.CreateLogger<DiffCommand>();
            var left = new Option<string>("-left")
            {
                Name = "left",
                Description = "path to the left profile",
                IsRequired = true
            };
            this.AddOption(left);

            var right = new Option<string>("-right")
            {
                Name = "right",
                Description = "path to the right profile",
                IsRequired = true
            };
            this.AddOption(right);

            var outFile = new Option<string>("-out")
            {
                Name = "out",
                Description = "path to the output (diff) file",
                IsRequired = true
            };
            this.AddOption(outFile);

            this.Handler = CommandHandler.Create<string, string, string>((left, right, outfile) => this.HandleCommand(left, right, outfile));

        }

        private int HandleCommand(string left, string right, string outFile)
        {
            // var tableResult = default(IEnumerable<SqlTable>); // Unused variable - commented out
            try
            {
                var rollup1 = File.ReadAllText(left).FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
                var rollup2 = File.ReadAllText(right).FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
                var tables = CdcDataUtilities.GetTables(_dac);

                var differ = new ProfileDiffer();
                var result = differ.Diff(tables, rollup1, rollup2);
                File.WriteAllText(outFile, result.ToJson(true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"No diffidy for you");
                return 0;
            }

            return 1;
        }

    }
}

