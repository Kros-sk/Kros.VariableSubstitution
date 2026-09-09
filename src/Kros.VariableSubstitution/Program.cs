using GlobExpressions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Kros.VariableSubstitution
{
    internal class Program
    {
        private static ILoggerFactory _loggerFactory;
        private static ILogger _logger;
        public static int Main(string[] args)
        {
            _loggerFactory = CreateLoggerFactory();
            _logger = _loggerFactory.CreateLogger(string.Empty);

            var workingDirectory = new Option<string>(
                aliases: new string[] { "-w", "--workingDirectory" },
                description: "Working directory - required");

            var zipFilesOrDirectories = new Option<IEnumerable<string>>(
                new string[] { "--zipFilesOrDirectories", "-f" },
                getDefaultValue: () => new List<string> { "**/*.zip" },
                "Glob pattern to search a list of files or directories");

            var jsonTargetFiles = new Option<string>(
                new string[] { "--jsonTargetFiles", "-j" },
                getDefaultValue: () => "**/*.json",
                description: "Glob pattern to search the target Json files to replace");

            var tempDirectory = new Option<string>(
                new string[] { "--tempDirectory", "-t" },
                getDefaultValue: () => Path.GetTempPath(),
                description: "Path to temp directory. The tool does not use it with --fast.");

            var variables = new Option<IDictionary<string, string>>(
                new string[] { "--variables", "-v" },
                parseArgument: ParseVariables,
                description: "Variables. (var1=value1 var2=value2). Used for testing.");

            var fast = new Option<bool>(
                new string[] { "--fast" },
                getDefaultValue: () => false,
                description: "Substitute directly in the Zip package. The tool does not extract the package. "
                    + "Peak memory grows with the size of the package.");

            var rootCommand = new RootCommand
            {
                workingDirectory,
                zipFilesOrDirectories,
                jsonTargetFiles,
                tempDirectory,
                variables,
                fast
            };

            rootCommand.SetHandler<string, IEnumerable<string>, string, string, IDictionary<string, string>, bool>(
                (wd, zip, json, temp, var, useFast) => RunCommand(wd, zip, json, temp, var, useFast),
                workingDirectory,
                zipFilesOrDirectories,
                jsonTargetFiles,
                tempDirectory,
                variables,
                fast);

            rootCommand.Description = "Run variable substitution in Json files.";
            var result = ExitCodes.Ok;
            //Check, if working directory was specified
            if (args.Length == 0 || (!args.Contains("-w") && !args.Contains("--workingDirectory")))
            {
                rootCommand.Invoke("--help");
                result = ExitCodes.MissingWorkingDirectory;
            }
            else
            {
                try
                {
                    result = rootCommand.InvokeAsync(args).Result;
                }
                catch (AggregateException ex) when (ex.InnerException is InvalidVariableFormatException)
                {
                    _logger.LogError(ex.InnerException.Message, ex.InnerException);
                    result = ExitCodes.WrongVariablesFormat;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                    result = ExitCodes.UnknownError;
                }
            }
            _loggerFactory.Dispose();
            return result;
        }

        private static IDictionary<string, string> ParseVariables(ArgumentResult arguments)
        {
            var result = new Dictionary<string, string>();
            foreach (var item in arguments.Tokens)
            {
                var splitVar = item.Value.Split('=');
                if (splitVar.Length <= 1)
                {
                    throw new InvalidVariableFormatException(item.Value);
                }
                result.Add(splitVar[0], splitVar[1]);
            }
            return result;
        }

        private static void RunCommand(
            string workingDirectory,
            IEnumerable<string> zipFilesOrDirectories,
            string jsonTargetFiles,
            string tempDirectory,
            IDictionary<string, string> variables,
            bool fast)
        {
            PrintLogo();

            tempDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            PackageProcessor processor = new(_logger, jsonTargetFiles, tempDirectory, fast);

            foreach (string glob in zipFilesOrDirectories)
            {
                IEnumerable<string> files = Glob.FilesAndDirectories(workingDirectory, glob);
                IVariablesProvider variablesProvider = CreateVariablesProvider(variables);

                foreach (string file in files)
                {
                    string fullPath = Path.Combine(workingDirectory, file);
                    _logger.LogInformation(" ──────────────────────────────────────────────");
                    _logger.LogInformation($"├─ {file}");

                    if (Directory.Exists(fullPath))
                    {
                        processor.ProcessDirectory(fullPath, variablesProvider);
                    }
                    else if (Path.HasExtension(file)
                        && Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(fullPath))
                    {
                        processor.ProcessZip(fullPath, variablesProvider);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Path '{fullPath}' was intended for processing, but it is neither a Zip file nor a directory.");
                    }
                }
            }
        }

        private static IVariablesProvider CreateVariablesProvider(IDictionary<string, string> variables)
            => variables?.Count > 0 ? new VariablesProvider(variables) : new EnvironmentVariablesProvider();

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
