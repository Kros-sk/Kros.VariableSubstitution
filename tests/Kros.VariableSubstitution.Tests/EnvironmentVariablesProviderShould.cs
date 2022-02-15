using FluentAssertions;
using FluentAssertions.Execution;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kros.VariableSubstitution.Tests
{
    public class EnvironmentVariablesProviderShould
    {
        private Dictionary<string, string> _variables = new()
        {
            { "KrosTestVariable", "test value" },
            { "Kros.Test.Variable", "test value separated by a dots" },
            { "KROS_TEST_VARIABLE_FOO", "test value separated by a underscores" }
        };

        [Fact]
        public void GetEnvironmentVariables()
        {
            try
            {
                InitVariables();
                var provider = new EnvironmentVariablesProvider();

                using (new AssertionScope())
                {
                    IDictionary<string, string> variables = provider.GetVariables();

                    variables.Should().ContainKey("KrosTestVariable")
                        .WhoseValue.Should().Be("test value");
                    variables.Should().ContainKey("Kros.Test.Variable")
                        .WhoseValue.Should().Be("test value separated by a dots");
                    variables.Should().ContainKey("KROS.TEST.VARIABLE.FOO")
                        .WhoseValue.Should().Be("test value separated by a underscores");
                }
            }
            finally
            {
                ClearVariables();
            }
        }

        private void InitVariables()
        {
            foreach (var variable in _variables)
            {
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
            }
        }

        private void ClearVariables()
        {
            foreach (var variable in _variables)
            {
                Environment.SetEnvironmentVariable(variable.Key, null);
            }
        }
    }
}
