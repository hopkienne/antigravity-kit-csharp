using System.CommandLine;

namespace AgCsharp.Commands;

public static class ValidateCommand
{
    public static Command Create()
    {
        var command = new Command("validate", "Validate .agent folder structure and integrity");

        command.SetHandler(() =>
        {
            var currentDir = Directory.GetCurrentDirectory();
            var agentPath = Path.Combine(currentDir, ".agent");

            Console.WriteLine();
            Console.WriteLine("🔍 Antigravity C# Backend Kit - Validation");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();

            if (!Directory.Exists(agentPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ .agent folder not found!");
                Console.ResetColor();
                Console.WriteLine("   Run 'ag-csharp init' to create it.");
                Console.WriteLine();
                return;
            }

            var issues = new List<string>();
            var rulesPath = Path.Combine(agentPath, "rules");
            var skillsPath = Path.Combine(agentPath, "skills");
            var workflowsPath = Path.Combine(agentPath, "workflows");

            // Check directories exist
            if (!Directory.Exists(rulesPath))
                issues.Add("Missing 'rules' directory");
            if (!Directory.Exists(skillsPath))
                issues.Add("Missing 'skills' directory");
            if (!Directory.Exists(workflowsPath))
                issues.Add("Missing 'workflows' directory");

            // Check file counts
            var rulesCount = Directory.Exists(rulesPath) ? Directory.GetFiles(rulesPath, "*.md").Length : 0;
            var skillsCount = Directory.Exists(skillsPath) ? Directory.GetFiles(skillsPath, "*.md").Length : 0;
            var workflowsCount = Directory.Exists(workflowsPath) ? Directory.GetFiles(workflowsPath, "*.md").Length : 0;

            Console.WriteLine("📁 Structure check:");
            PrintCheck(Directory.Exists(rulesPath), $"rules/      ({rulesCount} files)");
            PrintCheck(Directory.Exists(skillsPath), $"skills/     ({skillsCount} files)");
            PrintCheck(Directory.Exists(workflowsPath), $"workflows/  ({workflowsCount} files)");
            Console.WriteLine();

            // Check for expected file counts
            if (rulesCount < 13)
                issues.Add($"Expected at least 13 rules, found {rulesCount}");
            if (skillsCount < 17)
                issues.Add($"Expected at least 17 skills, found {skillsCount}");
            if (workflowsCount < 8)
                issues.Add($"Expected at least 8 workflows, found {workflowsCount}");

            // Check for empty files
            CheckForEmptyFiles(rulesPath, issues);
            CheckForEmptyFiles(skillsPath, issues);
            CheckForEmptyFiles(workflowsPath, issues);

            Console.WriteLine("📊 Content check:");
            PrintCheck(rulesCount >= 13, $"Rules: {rulesCount}/13");
            PrintCheck(skillsCount >= 17, $"Skills: {skillsCount}/17");
            PrintCheck(workflowsCount >= 8, $"Workflows: {workflowsCount}/8");
            Console.WriteLine();

            if (issues.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Validation passed! Your .agent folder is complete.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  Found {issues.Count} issue(s):");
                Console.ResetColor();
                foreach (var issue in issues)
                {
                    Console.WriteLine($"   • {issue}");
                }
                Console.WriteLine();
                Console.WriteLine("   Run 'ag-csharp update' to fix these issues.");
            }
            Console.WriteLine();
        });

        return command;
    }

    private static void PrintCheck(bool passed, string message)
    {
        if (passed)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("   ✓ ");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("   ✗ ");
        }
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void CheckForEmptyFiles(string directory, List<string> issues)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.GetFiles(directory, "*.md"))
        {
            var info = new FileInfo(file);
            if (info.Length == 0)
            {
                issues.Add($"Empty file: {Path.GetFileName(file)}");
            }
        }
    }
}
