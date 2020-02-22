using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QFind
{
    public class ThreadData
    {
        public WaitHandle CancelEvent { get; }

        public string Filename { get; private set; }

        public string[] Lines { get; set; }

        public List<ResultInfo> Matches { get; } = new List<ResultInfo>();

        public Exception Exception { get; set; }

        public ThreadData(WaitHandle cancelEvent)
        {
            CancelEvent = cancelEvent;
        }

        public void Reset(string filename)
        {
            Filename = filename;
            Lines = null;
            Matches.Clear();
            Exception = null;
        }
    }
}
