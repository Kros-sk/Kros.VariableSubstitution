using System.Collections.Generic;

namespace Kros.VariableSubstitution.Tests
{
    internal static class VariablesProviderExtensions
    {
        public static IVariablesProvider AsProvider(this object value)
            => new VariablesProvider(value as IDictionary<string, string>);
    }
}
