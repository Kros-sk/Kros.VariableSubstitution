using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kros.VariableSubstitution
{
    internal static class ExitCodes
    {
        public const int Ok = 0;
        public const int MissingWorkingDirectory = 1;
        public const int WrongVariablesFormat = 2;
    }
}
