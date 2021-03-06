using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using ToolBelt;
using System.Diagnostics;

namespace Tools
{
    [CommandLineTitle("Text File Space/Tab Line Fixer Tool")]
    [CommandLineDescription("Text file tab/space reporter and fixer. " +
        "For C# source files, the tool reports on beginning-of-line tabs/spaces. " + 
        "All tabs not at the beginning of a line are replaced with spaces. " + 
        "Spaces/tabs inside C# multi-line strings are ignored.  Note that conversion " +
        "to tabs may still leave the file as mixed as some lines may have spaces that are " +
        "not a whole number multiple of the tabstop size.  In that case use the -round " +
        "option to remove smooth out the spurious spaces.")]
    [CommandLineCopyright("Copyright (c) John Lyon-Smith 2014")]
    public class SpacerTool : ToolBase
    {
        public enum Whitespace
        {
            Mixed,
            M = Mixed,
            Tabs,
            T = Tabs,
            Spaces,
            S = Spaces
        }

        private enum FileType 
        {
            CSharp,
            Other
        }

        private FileType fileType = FileType.Other;

        [CommandLineArgument("help", ShortName="?", Description="Shows this help")]
        public bool ShowUsage { get; set; }
        [CommandLineArgument("mode", ShortName="m", Description="The convert mode. One of mixed, tabs or spaces.  Default is to just display the files current state.",
            Initializer=typeof(SpacerTool), MethodName="ParseConvertMode")]
        public Whitespace? ConvertMode { get; set; }
        [DefaultCommandLineArgument(Description="The input file to analyze and convert", ValueHint="INPUTFILE")]
        public ParsedFilePath InputFileName { get; set; }
        [CommandLineArgument("output", ShortName="o", Description="An optional output file name.  Default is to use the input file.", ValueHint="OUTPUTFILE")]
        public ParsedFilePath OutputFileName { get; set; }
        [CommandLineArgument("tabsize", ShortName="t", Description="The tabsize to assume. Default is 4 spaces.", ValueHint="TABSIZE")]
        public int? TabSize { get; set; }
        [CommandLineArgument("round", ShortName="r", Description="When tabifying, round BOL spaces down to an exact number of tabs.")]
        public bool RoundToNearestTab { get; set; }

        public static Whitespace? ParseConvertMode(string arg)
        {
            return (Whitespace?)Enum.Parse(typeof(Whitespace), arg, true);
        }

        public override void Execute()
        {
            if (ShowUsage) 
            {
                WriteMessage(this.Parser.LogoBanner);
                WriteMessage(this.Parser.Usage);
                return;
            }
            
            if (InputFileName == null)
            {
                WriteError("A text file must be specified");
                return;
            }

            if (!File.Exists(InputFileName))
            {
                WriteError("The file '{0}' does not exist", InputFileName);
                return;
            }

            if (InputFileName.Extension == ".cs")
            {
                fileType = FileType.CSharp;
            }

            if (OutputFileName == null)
            {
                OutputFileName = InputFileName;
            }
            else
            {
                if (!ConvertMode.HasValue)
                {
                    WriteError("Must specify conversion mode with output file");
                    return;
                }
            }

            if (!TabSize.HasValue)
            {
                TabSize = 4;
            }

            List<string> lines = ReadFileLines();
            int beforeTabs;
            int beforeSpaces;

            if (fileType == FileType.CSharp)
                CountCSharpBolSpacesAndTabs(lines, out beforeTabs, out beforeSpaces);
            else
                CountBolSpacesAndTabs(lines, out beforeTabs, out beforeSpaces);

            if (ConvertMode.HasValue)
            {
                if (fileType == FileType.CSharp)
                    CSharpUntabify(lines);
                else
                    Untabify(lines);

                if (this.ConvertMode == Whitespace.Tabs)
                {
                    if (fileType == FileType.CSharp)
                        CSharpTabify(lines);
                    else
                        Tabify(lines);
                }
            }

            StringBuilder sb = new StringBuilder();
            Whitespace ws = GetWhitespaceType(beforeTabs, beforeSpaces);

            sb.AppendFormat("\"{0}\", {1}, {2}", 
                this.InputFileName, fileType == FileType.CSharp ? "c#" : "other", GetWhitespaceName(ws));

            if (this.ConvertMode.HasValue)
            {
                int afterTabs, afterSpaces;

                if (fileType == FileType.CSharp)
                    CountCSharpBolSpacesAndTabs(lines, out afterTabs, out afterSpaces);
                else
                    CountBolSpacesAndTabs(lines, out afterTabs, out afterSpaces);

                ws = GetWhitespaceType(afterTabs, afterSpaces);

                using (StreamWriter writer = new StreamWriter(this.OutputFileName))
                {
                    foreach (var line in lines)
                    {
                        writer.Write(line);
                    }
                }

                sb.AppendFormat(" -> \"{0}\", {1}", this.OutputFileName, GetWhitespaceName(ws));
            }

            WriteMessage(sb.ToString());
        }

        Whitespace GetWhitespaceType(int tabs, int spaces)
        {
            return (tabs > 0) ? (spaces > 0 ? Whitespace.Mixed : Whitespace.Tabs) : Whitespace.Spaces;
        }

        object GetWhitespaceName(Whitespace ws)
        {
            return Enum.GetName(typeof(Whitespace), ws).ToLower();
        }

        public List<string> ReadFileLines()
        {
            // Read the entire file
            string fileContents = File.ReadAllText(InputFileName);

            // Convert to a list of lines, preserving the end-of-lines
            List<string> lines = new List<string>();
            int s = 0;
            int i = 0;

            while (i < fileContents.Length)
            {
                char c = fileContents[i];
                char c1 = i < fileContents.Length - 1 ? fileContents[i + 1] : '\0';

                if (c == '\r')
                {
                    i++;

                    if (c1 == '\n')
                        i++;
                }
                else if (c == '\n')
                {
                    i++;
                }
                else
                {
                    i++;
                    continue;
                }

                lines.Add(fileContents.Substring(s, i - s));
                s = i;
            }

            if (s != i)
                lines.Add(fileContents.Substring(s, i - s));

            return lines;
        }

        public void CountCSharpBolSpacesAndTabs(List<string> lines, out int numBolTabs, out int numBolSpaces)
        {
            numBolSpaces = 0;
            numBolTabs = 0;
            bool inMultiLineString = false;

            foreach (string line in lines)
            {
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    char c1 = i < line.Length - 1 ? line[i + 1] : '\0';

                    if (inMultiLineString && c == '"' && c1 != '"')
                        inMultiLineString = false;
                    else if (c == ' ')
                        numBolSpaces++;
                    else if (c == '\t')
                        numBolTabs++;
                    else if (c == '@' && c1 == '"')
                    {
                        inMultiLineString = true;
                        i++;
                    }
                    else
                        break;
                }
            }
        }

        public void CSharpUntabify(List<string> lines)
        {
            // Expand tabs anywhere on a line, but not inside @"..." strings

            StringBuilder sb = new StringBuilder();
            bool inMultiLineString = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                bool inString = false;

                for (int j = 0; j < line.Length; j++)
                {
                    char c_1 = j > 0 ? line[j - 1] : '\0';
                    char c = line[j];
                    char c1 = j < line.Length - 1 ? line[j + 1] : '\0';

                    Debug.Assert(!(inString && inMultiLineString));

                    if (!inMultiLineString && c == '\t')
                    {
                        // Add spaces to next tabstop
                        int numSpaces = this.TabSize.Value - (sb.Length % this.TabSize.Value);

                        sb.Append(' ', numSpaces);
                    }
                    else if (!inMultiLineString && !inString && c == '"')
                    {
                        inString = true;
                        sb.Append(c);
                    }
                    else if (!inMultiLineString && !inString && c == '@' && c1 == '"')
                    {
                        inMultiLineString = true;
                        sb.Append(c);
                        j++;
                        sb.Append(c1);
                    }
                    else if (inString && c == '"' && c_1 != '\\')
                    {
                        inString = false;
                        sb.Append(c);
                    }
                    else if (inMultiLineString && c == '"' && c1 != '"')
                    {
                        inMultiLineString = false;
                        sb.Append(c);
                    }
                    else
                        sb.Append(c);
                }

                lines[i] = sb.ToString();
                sb.Clear();
            }
        }

        public void CSharpTabify(List<string> lines)
        {
            // Insert tabs for spaces, but only at the beginning of lines and not inside @"..." or "..." strings

            StringBuilder sb = new StringBuilder();
            bool inMultiLineString = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                bool inString = false;
                bool bol = true;
                int numBolSpaces = 0;

                for (int j = 0; j < line.Length; j++)
                {
                    char c_1 = j > 0 ? line[j - 1] : '\0';
                    char c = line[j];
                    char c1 = j < line.Length - 1 ? line[j + 1] : '\0';

                    if (!inString && !inMultiLineString && bol && c == ' ')
                    {
                        // Just count the spaces
                        numBolSpaces++;
                    }
                    else if (!inString && !inMultiLineString && bol && c != ' ')
                    {
                        bol = false;

                        sb.Append(new string('\t', numBolSpaces / this.TabSize.Value));

                        if (!RoundToNearestTab)
                            sb.Append(new string(' ', numBolSpaces % this.TabSize.Value));

                        // Process this character again as not BOL
                        j--;
                    } 
                    else if (!inMultiLineString && !inString && c == '"')
                    {
                        inString = true;
                        sb.Append(c);
                    }
                    else if (!inMultiLineString && !inString && c == '@' && c1 == '"')
                    {
                        inMultiLineString = true;
                        sb.Append(c);
                        j++;
                        sb.Append(c1);
                    }
                    else if (inString && c == '"' && c_1 != '\\')
                    {
                        inString = false;
                        sb.Append(c);
                    }
                    else if (inMultiLineString && c == '"' && c1 != '"')
                    {
                        inMultiLineString = false;
                        sb.Append(c);
                    }
                    else
                        sb.Append(c);
                }

                lines[i] = sb.ToString();
                sb.Clear();
            }
        }

        public void CountBolSpacesAndTabs(List<string> lines, out int numBolTabs, out int numBolSpaces)
        {
            numBolSpaces = 0;
            numBolTabs = 0;

            foreach (string line in lines)
            {
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];

                    if (c == ' ')
                        numBolSpaces++;
                    else if (c == '\t')
                        numBolTabs++;
                    else
                        break;
                }
            }
        }

        public void Untabify(List<string> lines)
        {
            // Expand tabs anywhere on a line
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];

                    if (c == '\t')
                    {
                        // Add spaces to next tabstop
                        int numSpaces = this.TabSize.Value - (sb.Length % this.TabSize.Value);

                        sb.Append(' ', numSpaces);
                    }
                    else
                        sb.Append(c);
                }

                lines[i] = sb.ToString();
                sb.Clear();
            }
        }

        public void Tabify(List<string> lines)
        {
            // Insert tabs where there are only spaces between two tab stops, but only at the beginning of lines

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                bool bol = true;
                int numBolSpaces = 0;

                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];

                    if (bol && c == ' ')
                    {
                        // Just count the spaces
                        numBolSpaces++;
                    }
                    else if (bol && c != ' ')
                    {
                        bol = false;

                        sb.Append(new string('\t', numBolSpaces / this.TabSize.Value));

                        if (!RoundToNearestTab)
                            sb.Append(new string(' ', numBolSpaces % this.TabSize.Value));

                        sb.Append(c);
                    }
                    else 
                        sb.Append(c);
                }

                lines[i] = sb.ToString();
                sb.Clear();
            }
        }
    }
}
