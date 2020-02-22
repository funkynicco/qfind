using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QFind
{
    public struct ResultInfo
    {
        public int Line { get; private set; }

        public int Offset { get; private set; }

        public int Length { get; private set; }

        public ResultInfo(int line, int offset, int length)
        {
            Line = line;
            Offset = offset;
            Length = length;
        }
    }
}
