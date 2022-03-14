using System;

namespace Kros.VariableSubstitution
{
    internal class InvalidVariableFormatException : Exception
    {
        public string Variable { get; }

        public InvalidVariableFormatException(string variable) : base($"Error while parsing --variables option: {variable}.")
        {
            Variable = variable;
        }
    }
}
