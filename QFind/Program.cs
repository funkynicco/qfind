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
        static int Main(string[] args)
        {
            var searchRegex = string.Empty;
            var ignoreCase = false;
            var includeHidden = false;
            var simple = false;
            var dirs = new List<string>() { "" };
            var findFilesMode = false;

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

                    Finder.ExtensionRegex = new Regex($"\\.({extensionRegex.ToString()})$", RegexOptions.IgnoreCase);
                }
                else if (args[i] == "--dirs")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("--dirs requires an argument that contains a list of comma separated directories to scan.");
                        return 1;
                    }

                    dirs.Clear();
                    dirs.AddRange(args[++i].Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                else if (args[i] == "-f")
                {
                    findFilesMode = true;
                }
                else
                    searchRegex = args[i];
            }

            using (var cancelEvent = new ManualResetEvent(false))
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cancelEvent.Set();
                };

                if (string.IsNullOrEmpty(searchRegex))
                {
                    Console.Write("Find regex> ");
                    searchRegex = Console.ReadLine();
                }

                if (cancelEvent.WaitOne(0) ||
                    searchRegex == null)
                    return 0;

                var stopwatch = Stopwatch.StartNew();

                var options = RegexOptions.None;
                if (ignoreCase)
                    options |= RegexOptions.IgnoreCase;

                Finder.SearchRegex = new Regex(searchRegex, options);

                var statistics = new Statistics();

                if (findFilesMode)
                {
                    // find files
                    foreach (var dir in dirs)
                    {
                        foreach (var match in FileFinder.Scan(cancelEvent, dir, Finder.SearchRegex, ref statistics))
                        {
                            if (match.Start > 0)
                                Console.Write(match.Fullpath.Substring(0, match.Start));

                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.Write(match.Filename.Substring(0, match.Length));
                            Console.ForegroundColor = ConsoleColor.Gray;

                            if (match.Start + match.Length < match.Fullpath.Length)
                                Console.Write(match.Fullpath.Substring(match.Start + match.Length));

                            Console.WriteLine();
                        }
                    }
                }
                else
                {
                    // scan inside files
                    var numberOfThreads = Environment.ProcessorCount;
                    var backlog = new ThreadData[numberOfThreads];
                    var backlog_threads = new Thread[numberOfThreads];
                    int backlog_count = 0;

                    for (int i = 0; i < backlog.Length; ++i)
                    {
                        backlog[i] = new ThreadData(cancelEvent);
                    }

                    foreach (var dir in dirs)
                    {
                        Finder.RecursiveFind(cancelEvent, dir, includeHidden, filename =>
                        {
                            statistics.TotalFilesScanned++;

                            backlog[backlog_count].Reset(filename);
                            (backlog_threads[backlog_count] = new Thread(Finder.ProcessFileThread)).Start(backlog[backlog_count]);
                            if (++backlog_count == backlog.Length)
                            {
                                for (int i = 0; i < backlog_count; ++i)
                                {
                                    backlog_threads[i].Join();
                                    if (!cancelEvent.WaitOne(0))
                                        Finder.ListMatches(ref statistics, backlog[i], simple, cancelEvent);
                                }

                                backlog_count = 0;
                            }
                        });
                    }

                    for (int i = 0; i < backlog_count; ++i)
                    {
                        backlog_threads[i].Join();
                        if (!cancelEvent.WaitOne(0))
                            Finder.ListMatches(ref statistics, backlog[i], simple, cancelEvent);
                    }
                }

                stopwatch.Stop();

                var str = "";

                var canceled = cancelEvent.WaitOne(0);

                Console.ForegroundColor = ConsoleColor.White;

                if (statistics.TotalMatches != 0)
                {
                    Console.WriteLine();

                    Console.BackgroundColor = canceled ? ConsoleColor.DarkYellow : ConsoleColor.DarkGreen;
                    if (findFilesMode)
                        str = $" {statistics.TotalMatches} matches found";
                    else
                        str = $" {statistics.TotalMatches} matches found in {statistics.Files} files";
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    str = $" No matches found";
                }

                str += $" ({statistics.TotalFilesScanned} files scanned - {stopwatch.Elapsed})";

                Console.Write(str.PadRight(Console.BufferWidth - 2) + "# ");
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();

                if (canceled)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("<Operation was canceled prematurely>");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }

            if (Debugger.IsAttached)
                Console.ReadKey(true);

            return 0;
        }
    }
}