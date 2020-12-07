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
    public class Finder
    {
        public static Regex ExtensionRegex = new Regex("\\.(txt|cpp|inl|h|c|cs|lua|inc|cfg|ini|json|csv|py|js|html|cshtml|css|ts|java)$", RegexOptions.IgnoreCase);
        public static Regex ExtensionExcludeRegex = null;
        public static Regex SearchRegex = null;

        public static void ProcessFileThread(object obj)
        {
            var threadData = obj as ThreadData;
            try
            {
                threadData.Lines = File.ReadAllLines(threadData.Filename);
            }
            catch (Exception ex)
            {
                threadData.Exception = ex;
                return;
            }

            for (int i = 0; i < threadData.Lines.Length; ++i)
            {
                if (threadData.CancelEvent.WaitOne(0))
                    break;

                var sb = new StringBuilder(threadData.Lines[i].Length);
                for (int j = 0; j < threadData.Lines[i].Length; ++j)
                {
                    char c = threadData.Lines[i][j];
                    if (c == '\t')
                        sb.Append("    ");
                    else
                        sb.Append(c);
                }

                threadData.Lines[i] = sb.ToString();

                var match = SearchRegex.Match(threadData.Lines[i]);
                if (!match.Success)
                    continue;

                threadData.Matches.Add(new ResultInfo(i + 1, match.Index, match.Length));
            }
        }

        public static void ListMatches(ref Statistics statistics, ThreadData threadData, bool simple, WaitHandle cancelEvent)
        {
            if (threadData.Exception != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Read error of file: {threadData.Filename}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"Exception: {threadData.Exception.Message}");
                return;
            }

            if (threadData.Matches.Count == 0)
                return;

            Console.WriteLine();

            ++statistics.Files;
            statistics.TotalMatches += threadData.Matches.Count;

            Console.BackgroundColor = ConsoleColor.DarkBlue;
            int x = Console.CursorLeft;
            int y = Console.CursorTop;

            for (int i = 0; i < Console.BufferWidth - 1; ++i)
            {
                Console.Write(' ');
            }

            Console.SetCursorPosition(x, y);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($" {threadData.Filename}");
            Console.ForegroundColor = ConsoleColor.Gray;

            var results_str = $"{threadData.Matches.Count} result{(threadData.Matches.Count == 1 ? "" : "s")} ";

            Console.Write(results_str.PadLeft(Console.BufferWidth - threadData.Filename.Length - 1));
            Console.BackgroundColor = ConsoleColor.Black;

            //Console.WriteLine();
            Console.WriteLine();

            int MaxPrintLength = Console.BufferWidth - 12;

            var printed_lines_hash = new HashSet<int>();
            var match_lines = new HashSet<int>();
            foreach (var match in threadData.Matches)
            {
                var i = match.Line - 1;
                match_lines.Add(i);
            }

            int previous_line = -1;

            foreach (var match in threadData.Matches)
            {
                if (cancelEvent.WaitOne(0))
                    break;

                var i = match.Line - 1;

                if (!simple)
                {
                    if (previous_line != -1 &&
                        previous_line != i)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        for (int j = 0; j < Console.BufferWidth - 1; ++j)
                        {
                            Console.Write('=');
                        }
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine();
                    }

                    for (int j = Math.Max(0, i - 5); j < i; ++j)
                    {
                        if (match_lines.Contains(j))
                            continue;

                        if (printed_lines_hash.Contains(j))
                            continue;

                        printed_lines_hash.Add(j);

                        Console.Write($"{(j + 1).ToString().PadLeft(6)}: ");
                        var part = threadData.Lines[j];
                        if (part.Length > MaxPrintLength - 3)
                            part = part.Substring(0, MaxPrintLength - 3) + "...";
                        Console.WriteLine(part);
                        previous_line = j + 1;
                    }
                }

                Console.Write($"{(i + 1).ToString().PadLeft(6)}: ");
                previous_line = i + 1;

                var chunk_before = threadData.Lines[i].Substring(0, match.Offset);
                var chunk = threadData.Lines[i].Substring(match.Offset, match.Length);
                var chunk_after = threadData.Lines[i].Substring(match.Offset + match.Length);

                var print_len = MaxPrintLength;
                print_len -= chunk.Length;
                if (print_len > 0)
                {
                    var len = Math.Min(print_len, chunk_before.Length + 3);
                    print_len -= len;

                    if (chunk_before.Length > len)
                        chunk_before = "..." + chunk_before.Substring(chunk_before.Length - Math.Max(0, (len - 3)));

                    if (chunk_after.Length > print_len)
                        chunk_after = chunk_after.Substring(0, Math.Max(0, print_len - 3)) + "...";
                }
                else
                {
                    chunk_before = "";
                    chunk_after = "";
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(chunk_before);
                Console.BackgroundColor = ConsoleColor.DarkMagenta;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(chunk);
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(chunk_after);
                Console.ForegroundColor = ConsoleColor.Gray;

                if (!simple)
                {
                    for (int j = i + 1; j < Math.Min(threadData.Lines.Length, i + 6); ++j)
                    {
                        if (match_lines.Contains(j))
                            break;

                        if (printed_lines_hash.Contains(j))
                            continue;

                        printed_lines_hash.Add(j);

                        Console.Write($"{(j + 1).ToString().PadLeft(6)}: ");
                        var part = threadData.Lines[j];
                        if (part.Length > MaxPrintLength - 3)
                            part = part.Substring(0, MaxPrintLength - 3) + "...";
                        Console.WriteLine(part);

                        previous_line = j + 1;
                    }
                }
            }
        }

        static bool CheckQFindFileFound(string folder)
        {
            // check for .qfind and read its content to determine whether to load this folder

            Win32.WIN32_FIND_DATA wfd;
            var hFind = Win32.FindFirstFileEx(
                Path.Combine(folder, ".qfind"),
                Win32.FINDEX_INFO_LEVELS.FindExInfoBasic,
                out wfd,
                Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                0);

            if (hFind == Win32.INVALID_HANDLE_VALUE)
                return false;

            Win32.FindClose(hFind);
            return true;
        }

        public static void RecursiveFind(WaitHandle cancelEvent, string folder, bool includeHidden, Action<string> resultAction)
        {
            if (CheckQFindFileFound(folder)) // TODO: read the content of it and use it as matching filter
                return;

            var filter = folder.Length == 0 ? "*" : folder + "\\*";
            Win32.WIN32_FIND_DATA wfd;
            var hFind = Win32.FindFirstFileEx(
                filter,
                Win32.FINDEX_INFO_LEVELS.FindExInfoBasic,
                out wfd,
                Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                Win32.FIND_FIRST_EX_LARGE_FETCH);

            if (hFind == Win32.INVALID_HANDLE_VALUE)
                return;

            do
            {
                if (cancelEvent.WaitOne(0))
                    break;

                if (wfd.cFileName == "." ||
                    wfd.cFileName == "..")
                    continue;

                if ((wfd.dwFileAttributes & Win32.FILE_ATTRIBUTE_HIDDEN) != 0 &&
                    !includeHidden)
                    continue;

                var path = Path.Combine(folder, wfd.cFileName);

                if ((wfd.dwFileAttributes & Win32.FILE_ATTRIBUTE_DIRECTORY) != 0)
                {
                    RecursiveFind(cancelEvent, path, includeHidden, resultAction);
                    continue;
                }

                if (!ExtensionRegex.IsMatch(wfd.cFileName))
                    continue;

                if (ExtensionExcludeRegex != null &&
                    ExtensionExcludeRegex.IsMatch(wfd.cFileName))
                    continue;

                resultAction(path);
            }
            while (Win32.FindNextFile(hFind, ref wfd));

            Win32.FindClose(hFind);
        }
    }
}
