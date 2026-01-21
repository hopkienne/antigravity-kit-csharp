using System.CommandLine;
using AgCsharp.Services;

namespace AgCsharp.Commands;

public static class ListCommand
{
    public static Command Create()
    {
        var rulesOption = new Option<bool>(
            aliases: ["--rules", "-r"],
            description: "List only rules");

        var skillsOption = new Option<bool>(
            aliases: ["--skills", "-s"],
            description: "List only skills");

        var workflowsOption = new Option<bool>(
            aliases: ["--workflows", "-w"],
            description: "List only workflows");

        var command = new Command("list", "List available rules, skills, and workflows")
        {
            rulesOption,
            skillsOption,
            workflowsOption
        };

        command.SetHandler((bool rules, bool skills, bool workflows) =>
        {
            Console.WriteLine();
            Console.WriteLine("📋 Antigravity C# Backend Kit - Content Catalog");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();

            var templateService = new TemplateService();
            var content = templateService.GetContentCatalog();

            var showAll = !rules && !skills && !workflows;

            if (showAll || rules)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("📘 RULES (13 files)");
                Console.ResetColor();
                Console.WriteLine("   Guidelines and standards for C# development");
                Console.WriteLine();
                foreach (var rule in content.Rules)
                {
                    Console.WriteLine($"   • {rule.Name}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"     {rule.Description}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }

            if (showAll || skills)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("🎯 SKILLS (17 files)");
                Console.ResetColor();
                Console.WriteLine("   Code generation templates and prompts");
                Console.WriteLine();
                foreach (var skill in content.Skills)
                {
                    Console.WriteLine($"   • {skill.Name}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"     {skill.Description}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }

            if (showAll || workflows)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("🔄 WORKFLOWS (8 files)");
                Console.ResetColor();
                Console.WriteLine("   Step-by-step processes for common tasks");
                Console.WriteLine();
                foreach (var workflow in content.Workflows)
                {
                    Console.WriteLine($"   • {workflow.Name}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"     {workflow.Description}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }

            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"📊 Total: {content.Rules.Count + content.Skills.Count + content.Workflows.Count} files");
            Console.WriteLine();

        }, rulesOption, skillsOption, workflowsOption);

        return command;
    }
}
