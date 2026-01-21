using System.CommandLine;
using AgCsharp.Services;

namespace AgCsharp.Commands;

public static class UpdateCommand
{
    public static Command Create()
    {
        var backupOption = new Option<bool>(
            aliases: ["--backup", "-b"],
            description: "Create backup of existing .agent folder before updating");

        var command = new Command("update", "Update existing .agent folder with latest templates")
        {
            backupOption
        };

        command.SetHandler(async (bool backup) =>
        {
            var currentDir = Directory.GetCurrentDirectory();
            var targetPath = Path.Combine(currentDir, ".agent");

            Console.WriteLine();
            Console.WriteLine("🔄 Antigravity C# Backend Kit - Update");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();

            if (!Directory.Exists(targetPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  No .agent folder found!");
                Console.ResetColor();
                Console.WriteLine("   Run 'ag-csharp init' first to create it.");
                Console.WriteLine();
                return;
            }

            if (backup)
            {
                var backupPath = $"{targetPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                Console.WriteLine($"📦 Creating backup at: {backupPath}");
                CopyDirectory(targetPath, backupPath);
            }

            Console.WriteLine("🗑️  Removing existing .agent folder...");
            Directory.Delete(targetPath, recursive: true);

            Console.WriteLine("📦 Extracting latest templates...");
            Console.WriteLine();

            var templateService = new TemplateService();
            var result = await templateService.ExtractTemplatesAsync(targetPath);

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Successfully updated .agent folder!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine($"📊 Updated: {result.TotalCount} files");
                Console.WriteLine($"   • {result.RulesCount} rules");
                Console.WriteLine($"   • {result.SkillsCount} skills");
                Console.WriteLine($"   • {result.WorkflowsCount} workflows");
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                Console.ResetColor();
            }
        }, backupOption);

        return command;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
