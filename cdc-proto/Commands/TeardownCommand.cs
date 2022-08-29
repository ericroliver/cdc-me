using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc;

namespace Softbase
{
    public class TeardownCommand : Command
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public TeardownCommand(SimpleDac dac, ILoggerFactory factory)
           : base("teardown", "remove cdc tracking from a database")
        {
            _dac = dac;
            _logger = factory.CreateLogger<TeardownCommand>();

            this.Handler = CommandHandler.Create(() => this.HandleCommand());

        }

        private int HandleCommand()
        {
            try
            {
                _logger.LogDebug("teardown");
                CdcDataUtilities.DisableCdcOnDatabase(_dac);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to turn CDC off exiting");
                return 0;
            }

            return 1;
        }

    }

}

