using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace QFind
{
    class Program
    {
        static Regex _extensionRegex = new Regex("\\.(txt|cpp|inl|h|c|cs|lua|inc|cfg|ini|json|csv|py|js|html|css|ts)$", RegexOptions.IgnoreCase);
        static Regex _searchRegex = null;

        struct ResultInfo
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

        class ThreadData
        {
            public string Filename { get; private set; }

            public string[] Lines { get; set; }

            public List<ResultInfo> Matches { get; } = new List<ResultInfo>();

            public void Reset(string filename)
            {
                Filename = filename;
                Lines = null;
                Matches.Clear();
            }
        }

        struct Statistics
        {
            public int TotalMatches { get; set; }

            public int Files { get; set; }

            public int TotalFilesScanned { get; set; }
        }

        static void ProcessFileThread(object obj)
        {
            var threadData = obj as ThreadData;
            threadData.Lines = File.ReadAllLines(threadData.Filename);

            for (int i = 0; i < threadData.Lines.Length; ++i)
            {
                var match = _searchRegex.Match(threadData.Lines[i]);
                if (!match.Success)
                    continue;

                threadData.Matches.Add(new ResultInfo(i + 1, match.Index, match.Length));
            }
        }

        static void ListMatches(ref Statistics statistics, ThreadData threadData, bool simple)
        {
            if (threadData.Matches.Count == 0)
                return;

            ++statistics.Files;
            statistics.TotalMatches += threadData.Matches.Count;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(threadData.Filename);
            Console.ForegroundColor = ConsoleColor.Gray;

            const int MaxPrintLength = 80;

            foreach (var match in threadData.Matches)
            {
                var i = match.Line - 1;
                if (!simple)
                {
                    for (int j = Math.Max(0, i - 5); j < i; ++j)
                    {
                        Console.Write($"{(j + 1).ToString().PadLeft(6)}: ");
                        var part = threadData.Lines[j];
                        if (part.Length > MaxPrintLength - 3)
                            part = part.Substring(0, MaxPrintLength - 3) + "...";
                        Console.WriteLine(part);
                    }
                }

                Console.Write($"{(i + 1).ToString().PadLeft(6)}: ");

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
                        Console.Write($"{(j + 1).ToString().PadLeft(6)}: ");
                        var part = threadData.Lines[j];
                        if (part.Length > MaxPrintLength - 3)
                            part = part.Substring(0, MaxPrintLength - 3) + "...";
                        Console.WriteLine(part);
                    }
                }
            }
        }

        static void RecursiveFind(string folder, bool includeHidden, Action<string> resultAction)
        {
            var filter = folder.Length == 0 ? "*" : folder + "\\*";
            Win32.WIN32_FIND_DATA wfd;
            var hFind = Win32.FindFirstFileEx(
                filter,
                Win32.FINDEX_INFO_LEVELS.FindExInfoBasic,
                out wfd,
                Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                Win32.FIND_FIRST_EX_LARGE_FETCH);

            if (hFind == IntPtr.Zero)
                return;

            do
            {
                if (wfd.cFileName == "." ||
                    wfd.cFileName == "..")
                    continue;

                if ((wfd.dwFileAttributes & Win32.FILE_ATTRIBUTE_HIDDEN) != 0 &&
                    !includeHidden)
                    continue;

                var path = Path.Combine(folder, wfd.cFileName);

                if ((wfd.dwFileAttributes & Win32.FILE_ATTRIBUTE_DIRECTORY) != 0)
                {
                    RecursiveFind(path, includeHidden, resultAction);
                    continue;
                }

                if (!_extensionRegex.IsMatch(wfd.cFileName))
                    continue;

                resultAction(path);
            }
            while (Win32.FindNextFile(hFind, ref wfd));

            Win32.FindClose(hFind);
        }

        static int Main(string[] args)
        {
            var searchRegex = string.Empty;
            var ignoreCase = false;
            var includeHidden = false;
            var simple = false;

            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-i")
                {
                    ignoreCase = true;
                }
                else if (args[i] == "-a")
                {
                    includeHidden = true;
                }
                else if (args[i] == "-s")
                {
                    simple = true;
                }
                else if (args[i] == "--ext")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("--ext requires an argument that contains a list of comma separated file extensions.");
                        return 1;
                    }

                    var matches = Regex.Matches(args[++i], "([a-z0-9_]+)", RegexOptions.IgnoreCase);
                    if (matches.Count == 0)
                    {
                        Console.WriteLine("--ext argument list is empty.");
                        return 1;
                    }

                    var extensionRegex = new StringBuilder();

                    foreach (Match match in matches)
                    {
                        if (extensionRegex.Length != 0)
                            extensionRegex.Append('|');

                        extensionRegex.Append(match.Groups[1].Value);
                    }

                    _extensionRegex = new Regex($"\\.({extensionRegex.ToString()})$", RegexOptions.IgnoreCase);
                }
                else
                    searchRegex = args[i];
            }

            if (string.IsNullOrEmpty(searchRegex))
            {
                Console.Write("Find regex> ");
                searchRegex = Console.ReadLine();
            }

            var stopwatch = Stopwatch.StartNew();

            var options = RegexOptions.None;
            if (ignoreCase)
                options |= RegexOptions.IgnoreCase;

            _searchRegex = new Regex(searchRegex, options);

            var numberOfThreads = Environment.ProcessorCount;
            var backlog = new ThreadData[numberOfThreads];
            var backlog_threads = new Thread[numberOfThreads];
            int backlog_count = 0;

            for (int i = 0; i < backlog.Length; ++i)
            {
                backlog[i] = new ThreadData();
            }

            var statistics = new Statistics();

            RecursiveFind("", includeHidden, (filename) =>
            {
                statistics.TotalFilesScanned++;

                backlog[backlog_count].Reset(filename);
                (backlog_threads[backlog_count] = new Thread(ProcessFileThread)).Start(backlog[backlog_count]);
                if (++backlog_count == backlog.Length)
                {
                    for (int i = 0; i < backlog_count; ++i)
                    {
                        backlog_threads[i].Join();
                        ListMatches(ref statistics, backlog[i], simple);
                    }

                    backlog_count = 0;
                }
            });

            for (int i = 0; i < backlog_count; ++i)
            {
                backlog_threads[i].Join();
                ListMatches(ref statistics, backlog[i], simple);
            }

            stopwatch.Stop();

            if (statistics.TotalMatches != 0)
            {
                Console.WriteLine();
                Console.Write($"{statistics.TotalMatches} matches found in {statistics.Files} files");
            }
            else
                Console.Write($"No matches found");

            Console.WriteLine($" ({statistics.TotalFilesScanned} files scanned - {stopwatch.Elapsed})");

            if (Debugger.IsAttached)
                Console.ReadKey(true);

            return 0;
        }
    }
}