using System.Collections.Generic;

namespace Kros.VariableSubstitution.Tests
{
    internal static class DummyVariablesProviders
    {
        public static IVariablesProvider AsProvider(this object value)
            => new VariablesProvider(value as IDictionary<string, string>);

        private class VariablesProvider : IVariablesProvider
        {
            private readonly IDictionary<string, string> _variables;

            public VariablesProvider(IDictionary<string, string> variables)
            {
                _variables = variables;
            }

            public IDictionary<string, string> GetVariables() => _variables;
        }
    }
}
