using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc;

namespace Softbase
{
    public class InitCommand : Command
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public InitCommand(SimpleDac dac, ILoggerFactory factory)
           : base("init", "initialize a database with cdc")
        {
            _dac = dac;
            _logger = factory.CreateLogger<InitCommand>();

            this.Handler = CommandHandler.Create(() => this.HandleCommand());

        }

        private int HandleCommand()
        {
            try
            {
                _logger.LogDebug("init command");
                Init(_dac, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error initializing cdc");
                return 0;
            }

            return 1;
        }

        private static void Init(SimpleDac dac, ILogger logger)
        {
            try
            {
                CdcDataUtilities.EnableCdcOnDatabase(dac);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Unable to turn CDC on exit");
                throw;
            }

            var tableResult = default(IEnumerable<SqlTable>);
            try
            {
                tableResult = CdcDataUtilities.GetTables(dac);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Unable to retrieve the list of tables");
                throw;
            }

            CdcDataUtilities.EnableTableCdc(dac, tableResult, logger);
        }
    }

}

