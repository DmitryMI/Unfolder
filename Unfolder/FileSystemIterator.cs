using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unfolder
{
    abstract class FileSystemIterator
    {
        public static void ForAllFiles(DirectoryInfo root, FileSystemIterator iterator)
        {
            var files = root.EnumerateFiles();
            foreach (var file in files)
            {
                iterator.ForFile(file);
            }

            var dirs = root.EnumerateDirectories();
            foreach (var dir in dirs)
            {
                ForAllFiles(dir, iterator);
                iterator.ForDirectory(dir);
            }
        }

        protected abstract void ForFile(FileInfo fileInfo);
        protected abstract void ForDirectory(DirectoryInfo directoryInfo);
    }
}
