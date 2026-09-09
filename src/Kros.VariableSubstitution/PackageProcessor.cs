using GlobExpressions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Kros.VariableSubstitution
{
    /// <summary>
    /// Substitutes variables in the Json files of a Zip package or a directory.
    /// </summary>
    internal sealed class PackageProcessor
    {
        private readonly ILogger _logger;
        private readonly string _jsonTargetFiles;
        private readonly string _tempDirectory;
        private readonly bool _fast;
        private readonly Glob _targetGlob;

        public PackageProcessor(ILogger logger, string jsonTargetFiles, string tempDirectory, bool fast)
        {
            _logger = logger;
            _jsonTargetFiles = jsonTargetFiles;
            _tempDirectory = tempDirectory;
            _fast = fast;
            _targetGlob = new Glob(jsonTargetFiles, GlobOptions.CaseInsensitive);
        }

        /// <summary>
        /// Substitutes variables in the Json files inside <paramref name="zipPath"/>.
        /// </summary>
        /// <returns><see langword="true"/> when the archive was changed.</returns>
        public bool ProcessZip(string zipPath, IVariablesProvider variables)
            => _fast ? ProcessZipInPlace(zipPath, variables) : ProcessZipByRebuild(zipPath, variables);

        /// <summary>
        /// Substitutes variables in the Json files inside <paramref name="directory"/>.
        /// </summary>
        /// <returns><see langword="true"/> when at least one file was changed.</returns>
        public bool ProcessDirectory(string directory, IVariablesProvider variables)
        {
            IEnumerable<string> files = Glob.FilesAndDirectories(directory, _jsonTargetFiles);
            JsonVariableSubstituter substituter = new(_logger);
            bool wasSubstituted = false;

            foreach (string file in files)
            {
                _logger.LogInformation($"├─── {file}");
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

        /// <summary>
        /// Extracts the archive, substitutes, and builds a new archive from the extracted files.
        /// Memory stays flat regardless of package size, at the cost of writing the whole package to disk twice.
        /// </summary>
        private bool ProcessZipByRebuild(string zipPath, IVariablesProvider variables)
        {
            string dest = Path.Combine(_tempDirectory, Path.GetFileNameWithoutExtension(zipPath));
            try
            {
                ZipFile.ExtractToDirectory(zipPath, dest, true);
                if (ProcessDirectory(dest, variables))
                {
                    File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(dest, zipPath);
                    return true;
                }

                return false;
            }
            finally
            {
                if (Directory.Exists(dest))
                {
                    Directory.Delete(dest, true);
                }
            }
        }

        /// <summary>
        /// Rewrites only the Json entries of the archive. Entries that are not substituted keep their
        /// original compressed bytes, and an archive where nothing matched is not rewritten at all.
        /// Peak memory grows with the size of the package, because the archive is buffered to be rewritten.
        /// </summary>
        private bool ProcessZipInPlace(string zipPath, IVariablesProvider variables)
        {
            JsonVariableSubstituter substituter = new(_logger);
            Dictionary<string, string> pending = new();

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!_targetGlob.IsMatch(entry.FullName))
                    {
                        continue;
                    }

                    _logger.LogInformation($"├─── {entry.FullName}");

                    string source;
                    using (StreamReader reader = new(entry.Open()))
                    {
                        source = reader.ReadToEnd();
                    }

                    SubstitutionResult result = substituter.Substitute(variables, source);
                    if (result.WasSubstituted)
                    {
                        pending[entry.FullName] = result.Result;
                    }
                }
            }

            if (pending.Count == 0)
            {
                return false;
            }

            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
            {
                foreach (KeyValuePair<string, string> item in pending)
                {
                    ZipArchiveEntry entry = archive.GetEntry(item.Key);
                    if (entry is null)
                    {
                        continue;
                    }

                    using Stream stream = entry.Open();
                    stream.SetLength(0);
                    using StreamWriter writer = new(stream);
                    writer.Write(item.Value);
                }
            }

            return true;
        }
    }
}
