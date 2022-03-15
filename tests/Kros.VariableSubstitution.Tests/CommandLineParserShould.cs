using System;
using System.IO;
using Xunit;
namespace Kros.VariableSubstitution.Tests
{
    public class CommandLineParserShould
    {
        [Fact]
        public void MissingParametersOutput()
        {
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            Program.Main(new string[] { });
            var output = stringWriter.ToString();
            Assert.Contains("Run variable substitution in Json files.", output);
        }

        [Fact]
        public void WrongVariableFormatOutput()
        {
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            Program.Main(new string[] { "-w", "placeholder", "-v", "incorrectVariable" });
            var output = stringWriter.ToString();
            Assert.Contains("Error while parsing", output);
        }
    }
}
