using System.CommandLine;
using System.Reflection;

namespace AgCsharp.Commands;

public static class VersionCommand
{
    public static Command Create()
    {
        var command = new Command("version", "Display tool version information");

        command.SetHandler(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? version;

            Console.WriteLine();
            Console.WriteLine("🚀 Antigravity C# Backend Developer Kit");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();
            Console.WriteLine($"   Version:     {informationalVersion}");
            Console.WriteLine($"   Runtime:     {Environment.Version}");
            Console.WriteLine($"   OS:          {Environment.OSVersion}");
            Console.WriteLine();
            Console.WriteLine("   📦 Package:   Antigravity.CSharp.BackendKit");
            Console.WriteLine("   🔧 Command:   ag-csharp");
            Console.WriteLine();
            Console.WriteLine("   📚 Content:");
            Console.WriteLine("      • 13 Rules    (C# standards, architecture, security)");
            Console.WriteLine("      • 17 Skills   (Code generation templates)");
            Console.WriteLine("      • 8 Workflows (Development processes)");
            Console.WriteLine();
            Console.WriteLine("   🔗 Repository: https://github.com/antigravity/csharp-backend-kit");
            Console.WriteLine();
        });

        return command;
    }
}
