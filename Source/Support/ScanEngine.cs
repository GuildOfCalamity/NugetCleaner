using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Con = System.Diagnostics.Debug;

namespace NugetCleaner.Support
{
    /// <summary>
    /// Data model for folder matches during scan process.
    /// </summary>
    public class TargetItem
    {
        public string? Location { get; set; }
        public DateTime LastAccess { get; set; }
        public long Size { get; set; }
    }

    public class ScanEngine
    {
        public static event Action<long>? OnScanComplete = (size) => { };
        public static event Action<Exception>? OnScanError = (ex) => { };
        public static event Action<TargetItem>? OnTargetAdded = (ti) => { };

        /// <summary>
        /// The main caller method.
        /// </summary>
        /// <param name="path">root nuget package location</param>
        /// <param name="days">if less than this value will be considered a positive match</param>
        /// <param name="reportOnly">if true only report, if false delete</param>
        /// <param name="token">cancellation token</param>
        public static void Run(string path, int days, bool reportOnly, CancellationToken token = default)
        {
            if (reportOnly)
                Report(path, days, token);
            else
                Remove(path, days, token);
        }

        /// <summary>
        /// Traverses the <paramref name="Path"/> and reports via <see cref="TargetItem"/>.
        /// </summary>
        static void Report(string Path, int Days, CancellationToken token)
        {
            long totalSize = 0;
            try
            {
                foreach (string pkg in Directory.GetDirectories(Path))
                {
                    if (token.IsCancellationRequested) { break; }
                    foreach (string pkgVersion in Directory.GetDirectories(pkg))
                    {
                        if (token.IsCancellationRequested) { break; }
                        var lastAccess = RecursiveFindLastAccessTime(pkgVersion, DateTime.MinValue);
                        var dirAge = DateTime.Now - lastAccess;
                        if (dirAge.TotalDays >= Days)
                        {
                            var size = CalculateFolderSize(pkgVersion);
                            OnTargetAdded?.Invoke(new TargetItem { Location = pkgVersion, LastAccess = lastAccess, Size = size });
                            totalSize += size;
                        }
                    }
                }
                // $"Reclaimed size if deleted: {totalSize.HumanReadableSize()}"
                OnScanComplete?.Invoke(totalSize);
            }
            catch (Exception ex)
            {
                Con.WriteLine($"[ERROR] {ex.Message}");
                OnScanError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Traverses the <paramref name="Path"/> and deletes via <see cref="TargetItem"/>.
        /// </summary>
        static void Remove(string Path, int Days, CancellationToken token)
        {
            long totalSize = 0;
            try
            {
                foreach (string pkg in Directory.GetDirectories(Path))
                {
                    if (token.IsCancellationRequested) { break; }
                    foreach (string pkgVersion in Directory.GetDirectories(pkg))
                    {
                        if (token.IsCancellationRequested) { break; }
                        var lastAccess = RecursiveFindLastAccessTime(pkgVersion, DateTime.MinValue);
                        var dirAge = DateTime.Now - lastAccess;
                        if (dirAge.TotalDays >= Days)
                        {
                            var size = CalculateFolderSize(pkgVersion);
                            OnTargetAdded?.Invoke(new TargetItem { Location = pkgVersion, LastAccess = lastAccess, Size = size });
                            RecursiveDelete(pkgVersion);
                            totalSize += size;
                            if (Directory.GetDirectories(pkg).Length == 0)
                            {
                                Directory.Delete(pkg);
                            }
                        }
                    }
                }
                // $"Total bytes reclaimed: {totalSize.HumanReadableSize()}"
                OnScanComplete?.Invoke(totalSize);
            }
            catch (Exception ex)
            {
                Con.WriteLine($"[ERROR] {ex.Message}");
                OnScanError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Recursively calculates the total size of all files in the specified folder and its subfolders.
        /// </summary>
        /// <param name="folderPath">The path to the folder.</param>
        /// <returns>The total size of all files in bytes.</returns>
        static long CalculateFolderSize(string folderPath)
        {
            long totalSize = 0;
            try
            {
                // Get the size of all files in the current directory.
                foreach (var file in Directory.GetFiles(folderPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        Con.WriteLine($"Error accessing file: {file}. {ex.Message}");
                        OnScanError?.Invoke(ex);
                    }
                }

                // Recursively calculate size for subdirectories.
                foreach (var subfolder in Directory.GetDirectories(folderPath))
                {
                    try
                    {
                        totalSize += CalculateFolderSize(subfolder);
                    }
                    catch (Exception ex)
                    {
                        Con.WriteLine($"[ERROR] Accessing folder: {subfolder}. {ex.Message}");
                        OnScanError?.Invoke(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Con.WriteLine($"[ERROR] Accessing folder: {folderPath}. {ex.Message}");
                OnScanError?.Invoke(ex);
            }
            return totalSize;
        }

        static void RecursiveDelete(string dir)
        {
            foreach (string subdir in Directory.GetDirectories(dir))
            {
                try { RecursiveDelete(subdir); }
                catch (Exception ex) { OnScanError?.Invoke(ex); }
            }
            foreach (string subdir in Directory.GetDirectories(dir))
            {
                try { Directory.Delete(subdir); }
                catch (Exception ex) { OnScanError?.Invoke(ex); }
            }
            foreach (string f in Directory.GetFiles(dir))
            {
                try { File.Delete(f); }
                catch (Exception ex) { OnScanError?.Invoke(ex); }
            }
            try { Directory.Delete(dir); }
            catch (Exception ex) { OnScanError?.Invoke(ex); }
        }

        static DateTime RecursiveFindLastAccessTime(string dir, DateTime dt)
        {
            try
            {
                foreach (string subdir in Directory.GetDirectories(dir))
                {
                    dt = RecursiveFindLastAccessTime(subdir, dt);
                }
                foreach (string f in Directory.GetFiles(dir))
                {
                    if (dt < File.GetLastAccessTime(f))
                    {
                        dt = File.GetLastAccessTime(f);
                    }
                }
                return dt;
            }
            catch (Exception) { return DateTime.Now; }
        }
    }
}
