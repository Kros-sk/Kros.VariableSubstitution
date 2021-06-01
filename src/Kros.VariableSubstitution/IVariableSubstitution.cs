using System.IO;
using System.Threading.Tasks;

namespace Kros.VariableSubstitution
{
    /// <summary>
    /// Interface which describe class for variable substitution.
    /// </summary>
    internal interface IVariableSubstitution
    {
        /// <summary>
        /// Substitutes the specified variables.
        /// </summary>
        /// <param name="variables">The variables.</param>
        /// <param name="sourceFile">The source file.</param>
        Task SubstituteAsync(IVariablesProvider variables, StreamReader sourceFile);
    }
}
