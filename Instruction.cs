using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmSearch
{
    internal record Instruction(string Text, ulong Address, int Length)
    {
    }
}
