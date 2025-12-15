using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc;

namespace Softbase
{
    public class ProfileCommand : Command
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public ProfileCommand(SimpleDac dac, ILoggerFactory factory)
           : base("profile", "Generate a data profile")
        {
            _dac = dac;
            _logger = factory.CreateLogger<ProfileCommand>();
            var outFile = new Option<string>("-out")
            {
                Name = "out",
                Description = "path to write the out (profile)",
                IsRequired = true
            };

            this.AddOption(outFile);

            this.Handler = CommandHandler.Create<string>((outFile) => this.HandleCommand(outFile));

        }

        private int HandleCommand(string outFile)
        {
            var tableResult = default(IEnumerable<SqlTable>);
            try
            {
                tableResult = CdcDataUtilities.GetTables(_dac);
                var profile = CdcDataUtilities.BuildNetProfile(_dac, tableResult, _logger);
                File.WriteAllText(outFile, profile.ToJson(true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error generating net profile");
                return 0;
            }

            return 1;
        }

    }
}

