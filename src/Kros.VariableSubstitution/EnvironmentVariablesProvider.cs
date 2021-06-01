using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Kros.VariableSubstitution
{
    /// <summary>
    /// Get variables from environment.
    /// </summary>
    internal class EnvironmentVariablesProvider : IVariablesProvider
    {
        /// <inheritdoc />
        public IDictionary<string, string> GetVariables()
            => Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .ToDictionary(p => ((string)p.Key).Replace("_", "."), p => (string)p.Value);
    }
}
