using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace Kros.VariableSubstitution.Tests
{
    public class MainReturnShould
    {
        [Theory]
        [MemberData(nameof(GetArgs))]
        public void TestDifferentArgs(string[] args, int expectedReturn)
        {
            var actual = Program.Main(args);
            actual.Should().Be(expectedReturn);
        }

        public static IEnumerable<object[]> GetArgs()
        {
            yield return new object[] { new string[] { }, ExitCodes.MissingWorkingDirectory };
            yield return new object[] { new string[] { "-w", "placeholder", "-v", "incorrectVariable" }, ExitCodes.WrongVariablesFormat };
        }
    }
}
