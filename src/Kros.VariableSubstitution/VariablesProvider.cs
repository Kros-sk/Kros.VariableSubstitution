using System.Collections.Generic;

namespace Kros.VariableSubstitution
{
    internal class VariablesProvider : IVariablesProvider
    {
        private readonly IDictionary<string, string> _variables;

        public VariablesProvider(IDictionary<string, string> variables)
        {
            _variables = variables;
        }

        public IDictionary<string, string> GetVariables() => _variables;
    }
}
