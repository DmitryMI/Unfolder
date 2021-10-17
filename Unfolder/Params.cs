using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Unfolder
{
    enum Mode
    {
        None,
        Unfold,
        Refold
    }

    class Params
    {
        public string RootPath { get; private set; }
        public Mode WorkMode { get; private set; } = Mode.None;
        public bool UseAbsolutePathsForRefolding { get; private set; }
        public bool PrintHelp { get; private set; }
        public int Verbosity { get; private set; }

        public bool PauseOnFinish { get; private set; }

        public string GetHelp()
        {
            StringBuilder stringBuilder = new StringBuilder($"Unfolder v{Assembly.GetExecutingAssembly().GetName().Version}\n");

            stringBuilder.Append("Parameter style: -M -N -W or -MNW, both possible\n");
            stringBuilder.Append("Unfolder.exe <params> </path/to/target>. Without '<' and '>'\n");
            stringBuilder.Append("\n");
            stringBuilder.Append("-h\tPrint this help message and exit (no work will be done)\n");
            stringBuilder.Append("-u\tUnfolding mode. Mutually exclusive with -r\n");
            stringBuilder.Append("-r\tRefolding mode. Mutually exclusive with -u\n");
            stringBuilder.Append("-vX\tVerbosity level, X - number in range [0, 2]\n");
            stringBuilder.Append("-a\tUse absolute paths in .refolding file\n");
            stringBuilder.Append("-p\tPause upon finishing\n");

            return stringBuilder.ToString();
        }

        public static Params Parse(string[] args)
        {
            Params cmdParams = new Params();
            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument.StartsWith("-"))
                {
                    if (argument.Contains("u"))
                    {
                        if (cmdParams.WorkMode != Mode.None)
                        {
                            Console.WriteLine("Use -u or -r mutually exclusive");
                            return null;
                        }
                        cmdParams.WorkMode = Mode.Unfold;
                    }
                    if (argument.Contains("r"))
                    {
                        if (cmdParams.WorkMode != Mode.None)
                        {
                            Console.WriteLine("Use -u or -r mutually exclusive");
                            return null;
                        }
                        cmdParams.WorkMode = Mode.Refold;
                    }

                    if (argument.Contains("a"))
                    {
                        cmdParams.UseAbsolutePathsForRefolding = true;
                    }

                    if (argument.Contains("p"))
                    {
                        cmdParams.PauseOnFinish = true;
                    }

                    int vIndex = argument.IndexOf('v');
                    if (vIndex != -1)
                    {
                        char levelSymbol = argument[vIndex + 1];
                        bool parsingLevelOk = int.TryParse(levelSymbol.ToString(), out int level);
                        if (!parsingLevelOk)
                        {
                            Console.WriteLine("Verbosity level parsing failed");
                            return null;
                        }

                        cmdParams.Verbosity = level;
                    }

                    if (argument.Contains("h"))
                    {
                        cmdParams.PrintHelp = true;
                    }
                }
                else
                {
                    cmdParams.RootPath = argument;
                }
            }

            return cmdParams;
        }
    }
}
