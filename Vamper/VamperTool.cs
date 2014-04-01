using System;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;

namespace Tools
{
    public class VamperTool
    {
        private class FileType
        {
            public string name;
            public Regex[] fileSpecs;
            public Tuple<string, string>[] updates;
            public string write;
        }

        public bool HasOutputErrors { get; set; }

        public bool ShowUsage { get; set; }

        public bool DoUpdate { get; set; }

        public string VersionFile;
        private int major;
        private int minor;
        private int build;
        private int revision;
        private int startYear;
        private string[] fileList;

        public VamperTool()
        {
        }

        public void Execute()
        {
            if (ShowUsage)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string name = assembly.FullName.Substring(0, assembly.FullName.IndexOf(','));
                object[] attributes = assembly.GetCustomAttributes(true);
                string version = ((AssemblyFileVersionAttribute)attributes.First(x => x is AssemblyFileVersionAttribute)).Version;
                string copyright = ((AssemblyCopyrightAttribute)attributes.First(x => x is AssemblyCopyrightAttribute)).Copyright;
                string title = ((AssemblyTitleAttribute)attributes.First(x => x is AssemblyTitleAttribute)).Title;
                string description = ((AssemblyDescriptionAttribute)attributes.First(x => x is AssemblyDescriptionAttribute)).Description;

                WriteMessage("{0}. Version {1}", title, version);
                WriteMessage("{0}.\n", copyright);
                WriteMessage("{0}\n", description);
                WriteMessage("Usage: mono {0}.exe ...\n", name);
                WriteMessage(@"Arguments:
    [-u]                Actually do the version stamp update.
    [-h] or [-?]        Show this help.
");
                return;
            }

            string versionFile = this.VersionFile;
            
            if (String.IsNullOrEmpty(versionFile))
            {
                versionFile = FindVersionFile();
            
                if (versionFile == null)
                {
                    WriteError("Unable to find a .version file in this or parent directories.");
                    return;
                }
            }
            else if (!File.Exists(versionFile))
            {
                WriteError("Version file '{0}' does not exist", versionFile);
                return;
            }
            
            string versionFileName = Path.GetFileName(versionFile);
            string projectName = versionFileName.Substring(0, versionFileName.IndexOf('.'));
            string versionConfigFile = versionFile + ".config";

            WriteMessage("Version file is '{0}'", versionFile);
            WriteMessage("Version config file is '{0}'", versionConfigFile);
            WriteMessage("Project name is '{0}'", projectName);

            if (File.Exists(versionFile))
            {
                ReadVersionFile(versionFile);
            }
            else
            {
                major = 1;
                minor = 0;
                build = 0;
                revision = 0;
                startYear = DateTime.Now.Year;
                fileList = new string[] { };
            }
            
            int jBuild = ProjectDate(startYear);
            
            if (build != jBuild)
            {
                revision = 0;
                build = jBuild;
            }
            else
            {
                revision++;
            }

            WriteMessage("New version {0} be {1}.{2}.{3}.{4}", this.DoUpdate ? "will" : "would", major, minor, build, revision);
           
            if (this.DoUpdate)
                WriteMessage("Updating version information:");

            if (!File.Exists(versionConfigFile))
            {
                using (StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Tools.Default.version.config")))
                {
                    File.WriteAllText(versionConfigFile, reader.ReadToEnd());
                }
            }

            List<FileType> fileTypes = ReadVersionConfigFile(versionConfigFile);

            foreach (string file in fileList)
            {
                string path = Path.Combine(Path.GetDirectoryName(versionFile), file);
                string fileOnly = Path.GetFileName(file);
                bool match = false;

                foreach (var fileType in fileTypes)
                {
                    // Find files of this type
                    foreach (var fileSpec in fileType.fileSpecs)
                    {
                        if (fileSpec.IsMatch(fileOnly))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (!match)
                        // We did not find one, ignore it
                        continue;

                    // Are we just writing a file or updating an existing one?
                    if (String.IsNullOrEmpty(fileType.write))
                    {
                        if (!File.Exists(path))
                        {
                            WriteError("File '{0}' does note exist to update", path);
                            return;
                        }
                        
                        if (DoUpdate)
                        {
                            foreach (var update in fileType.updates)
                            {
                                string contents = File.ReadAllText(path);
    
                                contents = Regex.Replace(contents, update.Item1, update.Item2);

                                File.WriteAllText(path, contents);
                            }
                        }
                    }
                    else
                    {
                        string dir = Path.GetDirectoryName(path);
                        
                        if (!Directory.Exists(dir))
                        {
                            WriteError("Directory '{0}' does not exist to write file '{1}'", dir, Path.GetFileName(path));
                            return;
                        }
                        
                        if (DoUpdate)
                            File.WriteAllText(path, fileType.write);
                    }

                    break;
                }

                if (!match)
                {
                    WriteError("File '{0}' has no matching file type in the .version.config file", path);
                    return;
                }

                WriteMessage(path);
            }

            if (this.DoUpdate)
                WriteVersionFile(versionFile, fileList);
        }

        private static Regex WildcardToRegex(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$"); 
        }

        private string SubstituteVersions(string input)
        {
            StringBuilder sb = new StringBuilder(input);

            sb.Replace("${Major}", major.ToString());
            sb.Replace("${Minor}", minor.ToString());
            sb.Replace("${Build}", build.ToString());
            sb.Replace("${Revision}", revision.ToString());
            sb.Replace("${StartYear}", startYear.ToString());

            return sb.ToString();
        }

        private List<FileType> ReadVersionConfigFile(string versionConfigFileName)
        {
            XDocument versionConfigFile = XDocument.Load(versionConfigFileName);
            var fileTypes = new List<FileType>();

            foreach (var fileTypeElement in versionConfigFile.Descendants("FileType"))
            {
                var fileType = new FileType();

                fileType.name = (string)fileTypeElement.Element("Name");
                fileType.fileSpecs = fileTypeElement.Elements("FileSpec").Select<XElement, Regex>(x => WildcardToRegex((string)x)).ToArray();
                fileType.updates = fileTypeElement.Elements("Update").Select<XElement, Tuple<string, string>>(
                    x => new Tuple<string, string>((string)x.Element("Search"), SubstituteVersions((string)x.Element("Replace")))).ToArray();
                fileType.write = SubstituteVersions((string)fileTypeElement.Element("Write"));

                fileTypes.Add(fileType);
            }

            return fileTypes;
        }

        private void ReadVersionFile(string versionFileName)
        {
            XDocument versionDoc = XDocument.Load(versionFileName);

            major = (int)(versionDoc.Descendants("Major").First());
            minor = (int)(versionDoc.Descendants("Minor").First());
            build = (int)(versionDoc.Descendants("Build").First());
            revision = (int)(versionDoc.Descendants("Revision").First());
            startYear = (int)(versionDoc.Descendants("StartYear").First());

            fileList = versionDoc.Descendants("File").Select(x => SubstituteVersions((string)x)).ToArray();
        }

        private void WriteVersionFile(string versionFileName, string[] fileList)
        {
            XElement doc = 
                new XElement("Version",
                    new XElement("Files", fileList.Select(f => new XElement("File", f)).ToArray()),
                    new XElement("Major", major),
                    new XElement("Minor", minor),
                    new XElement("Build", build),
                    new XElement("Revision", revision),
                    new XElement("StartYear", startYear));

            doc.Save(versionFileName);
        }

        private string FindVersionFile()
        {
            var fileSpec = "*.version";
            string dir = Environment.CurrentDirectory;

            do
            {
                string[] files = Directory.GetFiles(dir, fileSpec);
            
                if (files.Length > 0)
                {
                    return files[0];
                }
            
                int i = dir.LastIndexOf(Path.DirectorySeparatorChar);
            
                if (i <= 0)
                    break;
            
                dir = dir.Substring(0, i);
            }
            while (true);

            return null;
        }

        static private int ProjectDate(int startYear)
        {
            DateTime today = DateTime.Today;
            
            return (((today.Year - startYear + 1) * 10000) + (today.Month * 100) + today.Day);
        }

        public void ProcessCommandLine(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    switch (arg[1])
                    {
                    case 'h':
                    case '?':
                        ShowUsage = true;
                        return;
                    case 'u':
                        DoUpdate = true;
                        return;
                    case 'f':
                        CheckAndSetArgument(arg, ref VersionFile);
                        return;
                    default:
                        throw new ApplicationException(string.Format("Unknown argument '{0}'", arg[1]));
                    }
                }
            }
        }

        private void CheckAndSetArgument(string arg, ref string val)
        {
            if (arg[2] != ':')
            {
                throw new ApplicationException(string.Format("Argument {0} is missing a colon", arg[1]));
            }
   
            if (string.IsNullOrEmpty(val))
            {
                val = arg.Substring(3);
            }
            else
            {
                throw new ApplicationException(string.Format("Argument {0} has already been set", arg[1]));
            }
        }

        private void WriteError(string format, params object[] args)
        {
            Console.Write("error: ");
            Console.WriteLine(format, args);
            this.HasOutputErrors = true;
        }

        private void WriteWarning(string format, params object[] args)
        {
            Console.Write("warning: ");
            Console.WriteLine(format, args);
            this.HasOutputErrors = true;
        }

        private void WriteMessage(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }
}

