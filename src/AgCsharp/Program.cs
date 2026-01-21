using System.CommandLine;
using AgCsharp.Commands;

namespace AgCsharp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Antigravity C# Backend Developer Kit - Initialize .agent configuration for AI-assisted development")
        {
            Name = "ag-csharp"
        };

        // Add commands
        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(UpdateCommand.Create());
        rootCommand.AddCommand(ListCommand.Create());
        rootCommand.AddCommand(ValidateCommand.Create());
        rootCommand.AddCommand(VersionCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
