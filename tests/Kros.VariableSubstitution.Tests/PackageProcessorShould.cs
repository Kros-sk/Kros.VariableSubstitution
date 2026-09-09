using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace Kros.VariableSubstitution.Tests
{
    public class PackageProcessorShould : IDisposable
    {
        private const string JsonTargetFiles = "**/appsettings.json";

        private readonly string _root;

        public PackageProcessorShould()
        {
            _root = Path.Combine(Path.GetTempPath(), "varsub-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SubstituteValueInRootLevelJson(bool fast)
        {
            string zip = CreatePackage("app.zip");

            bool substituted = CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "https://substituted")));

            substituted.Should().BeTrue();
            ReadJson(zip, "appsettings.json")["AppConfig"]["Endpoint"]
                .Value<string>().Should().Be("https://substituted");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SubstituteValueInNestedJson(bool fast)
        {
            string zip = CreatePackage("app.zip");

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "https://nested")));

            ReadJson(zip, "wwwroot/config/appsettings.json")["AppConfig"]["Endpoint"]
                .Value<string>().Should().Be("https://nested");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SubstituteEveryMatchingJsonInOneArchive(bool fast)
        {
            string zip = CreatePackage("app.zip");

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "https://both")));

            ReadJson(zip, "appsettings.json")["AppConfig"]["Endpoint"].Value<string>().Should().Be("https://both");
            ReadJson(zip, "wwwroot/config/appsettings.json")["AppConfig"]["Endpoint"].Value<string>().Should().Be("https://both");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LeaveNonTargetEntriesByteIdentical(bool fast)
        {
            string zip = CreatePackage("app.zip");
            Dictionary<string, byte[]> before = ReadAllEntries(zip);

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "https://substituted")));

            Dictionary<string, byte[]> after = ReadAllEntries(zip);
            foreach (string name in before.Keys.Where(k => !k.EndsWith("appsettings.json", StringComparison.Ordinal)))
            {
                after.Should().ContainKey(name);
                after[name].Should().Equal(before[name], $"entry '{name}' must not be altered");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PreserveTheCompleteEntryList(bool fast)
        {
            string zip = CreatePackage("app.zip");
            string[] before = ReadAllEntries(zip).Keys.OrderBy(k => k).ToArray();

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "https://substituted")));

            ReadAllEntries(zip).Keys.OrderBy(k => k).Should().Equal(before);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LeaveArchiveUntouchedWhenNothingMatches(bool fast)
        {
            string zip = CreatePackage("app.zip");
            byte[] before = File.ReadAllBytes(zip);

            bool substituted = CreateProcessor(fast).ProcessZip(zip, Variables(("NOTHING.MATCHES.THIS", "x")));

            substituted.Should().BeFalse();
            File.ReadAllBytes(zip).Should().Equal(before, "an archive with no substitutions must not be rewritten");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void KeepTheArchiveReadableAfterSubstitution(bool fast)
        {
            string zip = CreatePackage("app.zip");

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "https://substituted")));

            using ZipArchive archive = ZipFile.OpenRead(zip);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                buffer.Length.Should().Be(entry.Length, $"entry '{entry.FullName}' must fully decompress");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GrowTheJsonEntryWhenTheNewValueIsLonger(bool fast)
        {
            string zip = CreatePackage("app.zip");
            string longValue = new('x', 8_000);

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", longValue)));

            ReadJson(zip, "appsettings.json")["AppConfig"]["Endpoint"].Value<string>().Should().Be(longValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShrinkTheJsonEntryWhenTheNewValueIsShorter(bool fast)
        {
            string zip = CreatePackage("app.zip");

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "x")));

            ReadJson(zip, "appsettings.json")["AppConfig"]["Endpoint"].Value<string>().Should().Be("x");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SubstituteNonStringTypes(bool fast)
        {
            string zip = CreatePackage("app.zip");

            CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.USEFEATUREFLAGS", "true")));

            ReadJson(zip, "appsettings.json")["AppConfig"]["UseFeatureFlags"].Value<bool>().Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReportTheRealErrorForAnUnreadableArchive(bool fast)
        {
            string zip = Path.Combine(_root, "corrupt.zip");
            File.WriteAllBytes(zip, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF, 0xFF, 0xFF });

            Action act = () => CreateProcessor(fast).ProcessZip(zip, Variables(("APPCONFIG.ENDPOINT", "x")));

            act.Should().Throw<InvalidDataException>();
        }

        [Fact]
        public void ProduceTheSameContentWithFastAsWithoutIt()
        {
            string inPlace = CreatePackage("inplace.zip");
            string rebuilt = CreatePackage("rebuilt.zip");
            var variables = Variables(("APPCONFIG.ENDPOINT", "https://same"));

            CreateProcessor(fast: true).ProcessZip(inPlace, variables);
            CreateProcessor(fast: false).ProcessZip(rebuilt, variables);

            Dictionary<string, byte[]> a = ReadAllEntries(inPlace);
            Dictionary<string, byte[]> b = ReadAllEntries(rebuilt);

            a.Keys.OrderBy(k => k).Should().Equal(b.Keys.OrderBy(k => k));
            foreach (string name in a.Keys)
            {
                a[name].Should().Equal(b[name], $"--fast must match a full rebuild for '{name}'");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StillProcessAPlainDirectory(bool fast)
        {
            string dir = Path.Combine(_root, "loose");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "appsettings.json"), AppSettingsJson);

            bool substituted = CreateProcessor(fast)
                .ProcessDirectory(dir, Variables(("APPCONFIG.ENDPOINT", "https://loose")));

            substituted.Should().BeTrue();
            JObject.Parse(File.ReadAllText(Path.Combine(dir, "appsettings.json")))["AppConfig"]["Endpoint"]
                .Value<string>().Should().Be("https://loose");
        }

        private PackageProcessor CreateProcessor(bool fast)
            => new(NullLogger.Instance, JsonTargetFiles, Path.Combine(_root, "temp"), fast);

        private static IVariablesProvider Variables(params (string Key, string Value)[] variables)
            => new VariablesProvider(variables.ToDictionary(v => v.Key, v => v.Value));

        private const string AppSettingsJson = @"{
            ""AppConfig"": { ""Endpoint"": ""https://original"", ""UseFeatureFlags"": false },
            ""Logging"": { ""LogLevel"": { ""Default"": ""Information"" } }
        }";

        private string CreatePackage(string fileName)
        {
            string path = Path.Combine(_root, fileName);
            using var stream = new FileStream(path, FileMode.Create);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            WriteEntry(archive, "appsettings.json", AppSettingsJson);
            WriteEntry(archive, "wwwroot/config/appsettings.json", AppSettingsJson);
            WriteEntry(archive, "web.config", "<configuration />");
            WriteEntry(archive, "runtimeconfig.json", @"{ ""runtimeOptions"": { ""tfm"": ""net6.0"" } }");

            // Stand-ins for the DLLs that dominate a real service package.
            var random = new Random(1);
            foreach (string name in new[] { "Kros.Service.dll", "lib/Newtonsoft.Json.dll", "lib/System.Text.Json.dll" })
            {
                var bytes = new byte[64 * 1024];
                random.NextBytes(bytes);
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using Stream entryStream = entry.Open();
                entryStream.Write(bytes, 0, bytes.Length);
            }

            return path;
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        private static JObject ReadJson(string zipPath, string entryName)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            entry.Should().NotBeNull($"entry '{entryName}' should exist in the archive");
            using var reader = new StreamReader(entry.Open());
            return JObject.Parse(reader.ReadToEnd());
        }

        private static Dictionary<string, byte[]> ReadAllEntries(string zipPath)
        {
            var result = new Dictionary<string, byte[]>();
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                result.Add(entry.FullName, buffer.ToArray());
            }
            return result;
        }
    }
}
