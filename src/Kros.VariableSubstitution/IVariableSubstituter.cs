using System.IO;
using System.Threading.Tasks;

namespace Kros.VariableSubstitution
{
    /// <summary>
    /// Interface which describe class for variable substitution.
    /// </summary>
    internal interface IVariableSubstituter
    {
        /// <summary>
        /// Substitutes the specified variables.
        /// </summary>
        /// <param name="variables">The variables.</param>
        /// <param name="source">The source.</param>
        /// <returns>Modified response.</returns>
        string Substitute(IVariablesProvider variables, string source);
    }
}
