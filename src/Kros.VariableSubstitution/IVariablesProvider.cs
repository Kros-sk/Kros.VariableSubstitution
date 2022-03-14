using System.Collections.Generic;

namespace Kros.VariableSubstitution
{
    /// <summary>
    /// Provider for obtaining variables.
    /// </summary>
    internal interface IVariablesProvider
    {
        /// <summary>
        /// Get variables.
        /// </summary>
        IDictionary<string, string> GetVariables();
    }
}
