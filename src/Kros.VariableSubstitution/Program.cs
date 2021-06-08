using GlobExpressions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Kros.VariableSubstitution
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<string>(
                    new string[] { "--workingDirectory", "-w" },
                    description: "Working directory"),
                new Option<string>(
                    new string[] { "--zipFilesOrDirectories", "-f" },
                    () => "**/*.zip",
                    "Glob pattern to search a list of files or directories"),
                new Option<string>(
                    new string[] { "--jsonTargetFiles", "-j" },
                    () => "**/*.json",
                    description: "Glob pattern to search the target Json files"),
                new Option<string>(
                    new string[] { "--tempDirectory", "-t" },
                    () => Path.GetTempPath(),
                    description: "Path to temp directory"),
                new Option<IDictionary<string, string>>(
                    new string[] { "--variables", "-v"},
                    parseArgument: ParseVariables,
                    description: "Variables. (var1=value1 var2=value2)")
            };

            rootCommand.Description = "Run variable substitution in Json files.";

            rootCommand.Handler
                = CommandHandler.Create<string, string, string, string, IDictionary<string, string>>(RunCommand);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static IDictionary<string, string> ParseVariables(ArgumentResult arguments)
            => arguments.Tokens
                .Select(t => t.Value.Split('='))
                .ToDictionary(p => p[0], p => p[1]);

        private static void RunCommand(
            string workingDirectory,
            string zipFilesOrDirectories,
            string jsonTargetFiles,
            string tempDirectory,
            IDictionary<string, string> variables)
        {
            PrintLogo();

            tempDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            using ILoggerFactory loggerFactory = CreateLoggerFactory();
            ILogger logger = loggerFactory.CreateLogger(string.Empty);

            IEnumerable<string> files = Glob.FilesAndDirectories(workingDirectory, zipFilesOrDirectories);
            IVariablesProvider variablesProvider = CreateVariablesProvider(variables);

            foreach (string file in files)
            {
                string fullPath = Path.Combine(workingDirectory, file);
                logger.LogInformation(" ──────────────────────────────────────────────");
                logger.LogInformation($"├─ {file}");

                if (Directory.Exists(fullPath))
                {
                    ProcessDirectory(fullPath, jsonTargetFiles, variablesProvider, logger);
                }
                else if (Path.HasExtension(file)
                    && Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(fullPath))
                {
                    ProcessZipFile(jsonTargetFiles, tempDirectory, variablesProvider, file, fullPath, logger);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Path '{fullPath}' was intended for processing, but it is neither a Zip file nor a directory.");
                }
            }
        }

        private static IVariablesProvider CreateVariablesProvider(IDictionary<string, string> variables)
            => variables?.Count > 0 ? new VariablesProvider(variables) : new EnvironmentVariablesProvider();

        private static void ProcessZipFile(
            string jsonTargetFiles,
            string tempDirectory,
            IVariablesProvider variablesProvider,
            string file,
            string fullPath,
            ILogger logger)
        {
            string dest = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(file));
            try
            {
                ZipFile.ExtractToDirectory(fullPath, dest, true);
                if (ProcessDirectory(dest, jsonTargetFiles, variablesProvider, logger))
                {
                    File.Delete(fullPath);
                    ZipFile.CreateFromDirectory(dest, fullPath);
                }
            }
            finally
            {
                Directory.Delete(dest, true);
            }
        }

        private static bool ProcessDirectory(
            string directory,
            string jsonTargetFiles,
            IVariablesProvider variables,
            ILogger logger)
        {
            IEnumerable<string> files = Glob.FilesAndDirectories(directory, jsonTargetFiles);
            JsonVariableSubstituter substituter = new(logger);
            bool wasSubstituted = false;

            foreach (string file in files)
            {
                logger.LogInformation($"├─── {file}");
                string fullPath = Path.Combine(directory, file);
                SubstitutionResult result = substituter.Substitute(variables, File.ReadAllText(fullPath));
                if (result.WasSubstituted)
                {
                    wasSubstituted = true;
                    File.WriteAllText(fullPath, result.Result);
                }
            }

            return wasSubstituted;
        }

        private static ILoggerFactory CreateLoggerFactory() => LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
                options.TimestampFormat = "hh:mm:ss ";
            }).SetMinimumLevel(LogLevel.Information);
        });

        private static void PrintLogo()
        {
            Console.WriteLine(@"
  _  _______   ____   _____                 
 | |/ /  __ \ / __ \ / ____|                
 | ' /| |__) | |  | | (___     __ _   ___   
 |  < |  _  /| |  | |\___ \   / _` | / __|  
 | . \| | \ \| |__| |____) | | (_| |_\__ \_ 
 |_|\_\_|  \_\\____/|_____/   \__,_(_)___(_)
                                            
                                            
");
            Console.WriteLine(@"
____   ____            .__      ___.   .__                       ___.            __  .__  __          __  .__               
\   \ /   /____ _______|__|____ \_ |__ |  |   ____     ________ _\_ |__   ______/  |_|__|/  |_ __ ___/  |_|__| ____   ____  
 \   Y   /\__  \\_  __ \  \__  \ | __ \|  | _/ __ \   /  ___/  |  \ __ \ /  ___|   __\  \   __\  |  \   __\  |/  _ \ /    \ 
  \     /  / __ \|  | \/  |/ __ \| \_\ \  |_\  ___/   \___ \|  |  / \_\ \\___ \ |  | |  ||  | |  |  /|  | |  (  <_> )   |  \
   \___/  (____  /__|  |__(____  /___  /____/\___  > /____  >____/|___  /____  >|__| |__||__| |____/ |__| |__|\____/|___|  /
               \/              \/    \/          \/       \/          \/     \/                                          \/ 
");
        }
    }
}
