using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace QFind
{
    public struct FileFinderMatch
    {
        public string Fullpath { get; set; }

        public string Filename { get; set; }

        public int Start { get; set; }

        public int Length { get; set; }
    }

    public static class FileFinder
    {
        private static void Scan(WaitHandle cancelEvent, List<FileFinderMatch> result, string path, Regex regex, ref Statistics statistics)
        {
            var findPath = $"{Path.GetFullPath(string.IsNullOrEmpty(path) ? "." : path)}\\*.*";

            var findData = new Win32.WIN32_FIND_DATA();
            var hFind = Win32.FindFirstFileEx(
                findPath,
                Win32.FINDEX_INFO_LEVELS.FindExInfoBasic,
                out findData,
                Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                0);

            if (hFind == Win32.INVALID_HANDLE_VALUE)
                return;

            do
            {
                if (cancelEvent.WaitOne(0))
                    break;

                if (findData.cFileName != "." &&
                    findData.cFileName != "..")
                {
                    var subpath = Path.Combine(path, findData.cFileName);

                    if ((findData.dwFileAttributes & Win32.FILE_ATTRIBUTE_DIRECTORY) != 0)
                    {
                        Scan(cancelEvent, result, subpath, regex, ref statistics);
                    }
                    else
                    {
                        statistics.TotalFilesScanned++;

                        Match match;
                        if ((match = regex.Match(findData.cFileName)).Success)
                        {
                            statistics.TotalMatches++;
                            result.Add(new FileFinderMatch
                            {
                                Fullpath = subpath,
                                Filename = findData.cFileName,
                                Start = subpath.Length - findData.cFileName.Length + match.Index,
                                Length = match.Length
                            });
                        }
                    }
                }
            }
            while (Win32.FindNextFile(hFind, ref findData));

            Win32.FindClose(hFind);
        }

        public static IEnumerable<FileFinderMatch> Scan(WaitHandle cancelEvent, string path, Regex regex, ref Statistics statistics)
        {
            var result = new List<FileFinderMatch>();
            Scan(cancelEvent, result, path, regex, ref statistics);
            return result;
        }
    }
}
