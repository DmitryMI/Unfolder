using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Unfolder
{
    class Program
    {
        static void Main(string[] args)
        {
            Params cmdParams = Params.Parse(args);
            if (cmdParams == null)
            {
                return;
            }

            if (!Directory.Exists(cmdParams.RootPath))
            {
                Console.WriteLine($"Directory {cmdParams.RootPath} does not exist");
                Console.WriteLine(cmdParams.GetHelp());
                return;
            }

            if (cmdParams.PrintHelp)
            {
                Console.WriteLine(cmdParams.GetHelp());
                if (cmdParams.PauseOnFinish)
                {
                    Console.ReadKey();
                }
                return;
            }

            try
            {
                switch (cmdParams.WorkMode)
                {
                    case Mode.None:
                        Console.WriteLine("Work mode is unset. Use -u or -r to select mode");
                        Console.WriteLine(cmdParams.GetHelp());
                        break;
                    case Mode.Unfold:
                        Console.WriteLine($"Unfolding {cmdParams.RootPath}...");
                        Unfold(cmdParams);
                        break;
                    case Mode.Refold:
                        Console.WriteLine($"Refolding {cmdParams.RootPath}...");
                        Refold(cmdParams);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (ArgumentException ex)
            {
                var backColor = Console.BackgroundColor;
                var foreColor = Console.ForegroundColor;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Error:\n");
                Console.WriteLine(ex.Message);
                Console.BackgroundColor = backColor;
                Console.ForegroundColor = foreColor;
            }

            Console.WriteLine("Finished!");

            if (cmdParams.PauseOnFinish)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        

        class DirectoryRemover : FileSystemIterator
        {
            protected override void ForFile(FileInfo fileInfo)
            {
                
            }

            protected override void ForDirectory(DirectoryInfo directoryInfo)
            {
                Console.WriteLine($"Deleting directory {directoryInfo.FullName}...");
                Directory.Delete(directoryInfo.FullName);
            }
        }

        readonly struct PathPair
        {
            public string OldPath { get; }
            public string NewPath { get; }

            public PathPair(string old, string next)
            {
                OldPath = old;
                NewPath = next;
            }
        }


        class FileListCreator : FileSystemIterator
        {
            private readonly DirectoryInfo rootDirectoryInfo;
            private List<string> nextPaths = new List<string>();
            private List<string> oldPaths = new List<string>();
            private readonly Params cmdParams;

            public int Count => nextPaths.Count;
            public PathPair this[int i] => new PathPair(oldPaths[i], nextPaths[i]);
            public FileListCreator(DirectoryInfo root, Params cmdParams)
            {
                this.cmdParams = cmdParams;
                rootDirectoryInfo = root;
            }

            public DirectoryInfo RootDirectoryInfo => rootDirectoryInfo;

            private string GenerateNextPath(FileInfo fileInfo, DirectoryInfo root)
            {
                //Stack<string> filePathStack = new Stack<string>();
                Queue<string> filePathQueue = new Queue<string>();
                filePathQueue.Enqueue(fileInfo.Name);
                DirectoryInfo parent = fileInfo.Directory;

                while (parent != null && parent.FullName != root.FullName)
                {
                    filePathQueue.Enqueue(parent.Name);
                    parent = parent.Parent;
                }

                string nextPath;                

                StringBuilder nextPathBuilder = new StringBuilder();
                if (!cmdParams.UseShorterNames)
                {
                    while (true)
                    {
                        nextPathBuilder.Insert(0, filePathQueue.Dequeue());
                        if (filePathQueue.Count > 0)
                        {
                            nextPathBuilder.Insert(0, '-');
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    while (true)
                    {
                        nextPathBuilder.Insert(0, filePathQueue.Dequeue());
                        if (!File.Exists(Path.Combine(rootDirectoryInfo.FullName, nextPathBuilder.ToString())))
                        {
                            break;
                        }                       
                        if (filePathQueue.Count > 0)
                        {
                            nextPathBuilder.Insert(0, '-');
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                
                nextPath = nextPathBuilder.ToString();
                return nextPath;
            }

            protected override void ForFile(FileInfo fileInfo)
            {
                string nextPath = GenerateNextPath(fileInfo, rootDirectoryInfo);
                string nextPathAbsolute = Path.Combine(rootDirectoryInfo.FullName, nextPath);

                if (cmdParams.UseAbsolutePathsForRefolding)
                {
                    nextPaths.Add(nextPathAbsolute);
                    oldPaths.Add(fileInfo.FullName);
                }
                else
                {
                    string fullPath = fileInfo.FullName;
                    string rootPath = rootDirectoryInfo.FullName;
                    string relativePath = fullPath.Replace(rootPath, "");
                    if (relativePath[0] == '\\')
                    {
                        relativePath = relativePath.Remove(0, 1);
                    }
                    oldPaths.Add(relativePath);
                    nextPaths.Add(nextPath);
                }

                if (cmdParams.Verbosity >= 2)
                {
                    Console.WriteLine($"{fileInfo.FullName} -> {nextPathAbsolute}");
                }

                File.Move(fileInfo.FullName, nextPathAbsolute);
            }

            protected override void ForDirectory(DirectoryInfo directoryInfo)
            {
                
            }
        }

        static void GenerateRefoldingFile(FileListCreator fileListCreator, Params cmdParams)
        {
            XmlDocument doc = new XmlDocument();

            //(1) the xml declaration is recommended, but not mandatory
            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            //(2) string.Empty makes cleaner code
            XmlElement refoldingInfo = doc.CreateElement(string.Empty, "RefoldingInfo", string.Empty);

            XmlAttribute usesAbsolutePathsAttribute = doc.CreateAttribute("UsesAbsolutePaths");
            usesAbsolutePathsAttribute.Value = cmdParams.UseAbsolutePathsForRefolding.ToString();
            refoldingInfo.SetAttributeNode(usesAbsolutePathsAttribute);
            doc.AppendChild(refoldingInfo);

            for (int i = 0; i < fileListCreator.Count; i++)
            {
                var pair = fileListCreator[i];
                XmlElement refoldingElement = doc.CreateElement(string.Empty, "RefoldingEntry", string.Empty);
                XmlElement nextPathElement = doc.CreateElement(string.Empty, "NewPath", string.Empty);
                nextPathElement.InnerText = pair.NewPath;
                XmlElement oldPathElement = doc.CreateElement(string.Empty, "OldPath", string.Empty);
                oldPathElement.InnerText = pair.OldPath;
                refoldingElement.AppendChild(nextPathElement);
                refoldingElement.AppendChild(oldPathElement);
                refoldingInfo.AppendChild(refoldingElement);
            }

            string refoldingFilePath = Path.Combine(fileListCreator.RootDirectoryInfo.FullName, ".refolding");
            doc.Save(refoldingFilePath);
        }

        static XmlDocument ReadRefoldingFile(string refoldingFilePath)
        {
            XmlDocument document = new XmlDocument();
            document.Load(refoldingFilePath);
            return document;
        }


        static void Unfold(Params cmdParams)
        {
            DirectoryInfo root = new DirectoryInfo(cmdParams.RootPath);
            FileListCreator fileListCreator = new FileListCreator(root, cmdParams);
            FileSystemIterator.ForAllFiles(root, fileListCreator);
            GenerateRefoldingFile(fileListCreator, cmdParams);

            DirectoryRemover directoryRemover = new DirectoryRemover();
            FileSystemIterator.ForAllFiles(root, directoryRemover);
        }

        static void MoveFile(string from, string to, Params cmdParams, bool usesAbsolutePaths)
        {
            FileInfo targetFileInfo = new FileInfo(to);
            
            DirectoryInfo parent = targetFileInfo.Directory;
            
            while (parent != null && !parent.Exists)
            {
                if (cmdParams.Verbosity >= 1)
                {
                    Console.WriteLine($"Restoring directory {parent.FullName}");
                }

                Directory.CreateDirectory(parent.FullName);
                parent = parent.Parent;
            }

            if (cmdParams.Verbosity >= 2)
            {
                Console.WriteLine($"{from} -> {to}");
            }
            
            File.Move(from, to);
        }

        static void Refold(Params cmdParams)
        {
            DirectoryInfo root = new DirectoryInfo(cmdParams.RootPath);
            string refoldingFilePath = Path.Combine(root.FullName, ".refolding");
            if (!File.Exists(refoldingFilePath))
            {
                throw new ArgumentException("Refolding data not found!");
            }

            XmlDocument refoldingXmlDocument = ReadRefoldingFile(refoldingFilePath);

            XmlElement rootXmlElement = refoldingXmlDocument["RefoldingInfo"];
            if (rootXmlElement == null)
            {
                throw new ArgumentException("Refolding info file is malformed");
            }

            bool usesAbsolutePaths = false;
            XmlAttribute usesAbsolutePathsAttribute = rootXmlElement.Attributes["UsesAbsolutePaths"];
            if (usesAbsolutePathsAttribute != null)
            {
                usesAbsolutePaths = bool.Parse(usesAbsolutePathsAttribute.InnerText);
            }

            List<XmlElement> succeededEntriesList = new List<XmlElement>();

            foreach (XmlElement refoldingEntry in rootXmlElement)
            {
                XmlElement oldPathElement = refoldingEntry["OldPath"];
                XmlElement nextPathElement = refoldingEntry["NewPath"];
                if (oldPathElement == null || nextPathElement == null)
                {
                    throw new ArgumentException("Refolding info file is malformed");
                }
                string oldPath = oldPathElement.InnerText;
                string nextPath = nextPathElement.InnerText;

                if (!usesAbsolutePaths)
                {
                    nextPath = Path.Combine(cmdParams.RootPath, nextPath);
                    oldPath = Path.Combine(cmdParams.RootPath, oldPath);
                }

                if (File.Exists(nextPath))
                {
                    MoveFile(nextPath, oldPath, cmdParams, usesAbsolutePaths);
                    succeededEntriesList.Add(refoldingEntry);
                }
                else
                {
                    Console.WriteLine($"File {nextPath} not found!");
                }
            }

            foreach (var succeededEntry in succeededEntriesList)
            {
                rootXmlElement.RemoveChild(succeededEntry);
            }

            if (rootXmlElement.HasChildNodes)
            {
                var backColor = Console.BackgroundColor;
                var foreColor = Console.ForegroundColor;
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine("Some files were missing during refolding. Make sure that these files" +
                                  "are in the target folder and rerun Unfolder in Refolding mode. If you " +
                                  "do not need these files, simply ignore this message and delete " +
                                  ".refolding file\n");
                Console.BackgroundColor = backColor;
                Console.ForegroundColor = foreColor;

                refoldingXmlDocument.Save(refoldingFilePath);
            }
            else
            {
                File.Delete(refoldingFilePath);
            }
        }
    }
}
