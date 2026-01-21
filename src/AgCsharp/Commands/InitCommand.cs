using System.CommandLine;
using AgCsharp.Services;

namespace AgCsharp.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Overwrite existing .agent folder if it exists");

        var command = new Command("init", "Initialize .agent folder in the current directory")
        {
            forceOption
        };

        command.SetHandler(async (bool force) =>
        {
            var currentDir = Directory.GetCurrentDirectory();
            var targetPath = Path.Combine(currentDir, ".agent");

            Console.WriteLine();
            Console.WriteLine("🚀 Antigravity C# Backend Kit");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine();

            if (Directory.Exists(targetPath))
            {
                if (!force)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠️  .agent folder already exists!");
                    Console.ResetColor();
                    Console.WriteLine("   Use --force to overwrite existing configuration.");
                    Console.WriteLine();
                    return;
                }

                Console.WriteLine("🗑️  Removing existing .agent folder...");
                Directory.Delete(targetPath, recursive: true);
            }

            Console.WriteLine("📦 Extracting templates...");
            Console.WriteLine();

            var templateService = new TemplateService();
            var result = await templateService.ExtractTemplatesAsync(targetPath);

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Successfully initialized .agent folder!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("📁 Created structure:");
                Console.WriteLine($"   {targetPath}");
                Console.WriteLine($"   ├── rules/      ({result.RulesCount} files)");
                Console.WriteLine($"   ├── skills/     ({result.SkillsCount} files)");
                Console.WriteLine($"   └── workflows/  ({result.WorkflowsCount} files)");
                Console.WriteLine();
                Console.WriteLine($"📊 Total: {result.TotalCount} markdown files");
                Console.WriteLine();
                Console.WriteLine("💡 Your AI assistant will now use these rules, skills, and workflows");
                Console.WriteLine("   to provide C# backend development guidance.");
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                Console.ResetColor();
            }
        }, forceOption);

        return command;
    }
}
