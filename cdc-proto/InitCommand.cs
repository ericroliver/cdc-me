using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;

namespace Softbase
{
    public class InitCommand : Command
    {
        
        public InitCommand()
           : base("greet", "Says a greeting to the specified person.")
        {
            var name = new Option<string>("--name")
            {
                Name = "name",
                Description = "The name of the person to greet.",
                IsRequired = true
            };

            this.AddOption(name);

            this.Handler = CommandHandler.Create(
                (string name) => this.HandleCommand(name));

        }

        private int HandleCommand(string name)
        {
            try
            {
                Console.WriteLine($"{this.options.Greeting} {name}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 0;
            }

            return 1;
        }
    }


    public static class CliCommandCollectionExtensions
    {
        public static IServiceCollection AddCliCommands(this IServiceCollection services)
        {
            Type commandType = typeof(InitCommand);
            Type baseCommandType = typeof(Command);

            IEnumerable<Type> commands = commandType
                .Assembly
                .GetExportedTypes()
                .Where(x => x.Namespace == commandType.Namespace && baseCommandType.IsAssignableFrom(x));

            foreach (Type command in commands)
            {
                services.AddSingleton(baseCommandType, command);
            }

            return services;
        }
    }
}

