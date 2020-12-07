using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace QFind
{
    public static class Utilities
    {
        public static IEnumerable<string> ReadExtensionsList(string data)
        {
            var result = new List<string>();

            var matches = Regex.Matches(data, "([a-z0-9_]+)", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                result.Add(match.Groups[1].Value);
            }

            return result;
        }
    }
}
