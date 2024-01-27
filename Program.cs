using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Synchronize
{
    internal class Program
    {
        static string logFilePath;
        static void Main(string[] args)
        {
            string sourcePath;
            string replicaPath;
            int syncInterval;

            // Take the inputs
            Console.WriteLine("Enter source folder path: ");
            sourcePath = Console.ReadLine();

            Console.WriteLine("Enter replica folder path: ");
            replicaPath = Console.ReadLine();

            Console.WriteLine("Enter log file path: ");
            logFilePath = Console.ReadLine();

            Console.WriteLine("Enter the sync interval: ");
            bool intervalParseResult = int.TryParse(Console.ReadLine(), out syncInterval);

            Console.Title = "Synchronize";

            // Validate the inputs
            if(sourcePath.StartsWith("\"") || sourcePath.StartsWith("'"))
            {
                sourcePath = sourcePath.Substring(1, sourcePath.Length - 2);
            }
            //sourcePath = sourcePath.Replace("\"", "");
            if (replicaPath.StartsWith("\"") || replicaPath.StartsWith("'"))
            {
                replicaPath = replicaPath.Substring(1, replicaPath.Length - 2);
            }
            //replicaPath = replicaPath.Replace("\"", "");
            //logFilePath = logFilePath.Replace("\"", "");
            if (logFilePath.StartsWith("\"") || logFilePath.StartsWith("'"))
            {
                logFilePath = logFilePath.Substring(1, logFilePath.Length - 2);
            }

            if (sourcePath == null || string.IsNullOrWhiteSpace(sourcePath))
            {
                Log("Source path is null", 2);
                return;
            }

            if( ! (Directory.Exists(sourcePath)))
            {
                Log("Source folder doesn't exist: nothing to sync", 2);
                return;
            }

            if(replicaPath == null || string.IsNullOrWhiteSpace(replicaPath))
            {
                Log("Replica folder not specified: nowhere to sync", 2);
                return;
            }

            if (!(Directory.Exists(replicaPath)))
            {
                try
                {
                    Directory.CreateDirectory(replicaPath);
                    Log("Replica directory did not exist, created it", 0);
                } catch (Exception ex)
                {
                    Log(ex.Message,  2);
                    // Directory is not reachable/usable for some reason, quit
                    return;
                }
            }

            if(syncInterval < 1)
            {
                Log("Cannot sync at every 0 or less seconds, going for a default 30 seconds", 1);
                syncInterval = 30;
            }

            // Proceed with the sync
            
            while( true) { 
                // Index the source files and folders
                List<string> sourceDirectories = new List<string>();
                Dictionary<string, string> sourceFilesAndHashes = new Dictionary<string, string>();

                sourceDirectories = GetDirectoriesRecursive(sourcePath);

                Dictionary<string, string> sourceRootFiles = GetFilesAndHashes(sourcePath);
                foreach (KeyValuePair<string, string> fileInfo in sourceRootFiles)
                {
                    sourceFilesAndHashes.Add(fileInfo.Key, fileInfo.Value);
                }

                foreach (string dir in sourceDirectories)
                {
                    Dictionary<string, string> files = GetFilesAndHashes(dir);
                    foreach (KeyValuePair<string, string> fileInfo in files)
                    {
                        sourceFilesAndHashes.Add(fileInfo.Key, fileInfo.Value);
                    }
                }

                // Index the replica files and folders
                List<string> replicaDirectories = new List<string>();
                Dictionary<string, string> replicaFilesAndHashes = new Dictionary<string, string>();

                replicaDirectories = GetDirectoriesRecursive(replicaPath);

                Dictionary<string, string> replicaRootFiles = GetFilesAndHashes(replicaPath);
                foreach (KeyValuePair<string, string> fileInfo in replicaRootFiles)
                {
                    replicaFilesAndHashes.Add(fileInfo.Key, fileInfo.Value);
                }

                foreach (string dir in replicaDirectories)
                {
                    Dictionary<string, string> files = GetFilesAndHashes(dir);
                    foreach (KeyValuePair<string, string> fileInfo in files)
                    {
                        replicaFilesAndHashes.Add(fileInfo.Key, fileInfo.Value);
                    }
                }

                // Replicate the directory structure
                foreach (string sourceDir  in sourceDirectories)
                {
                    string expectedReplicaDir = sourceDir.Replace(sourcePath, replicaPath);
                    if(!(Directory.Exists(expectedReplicaDir)))
                    {
                        Directory.CreateDirectory(expectedReplicaDir);
                    }
                }

                // Replicate the files (if not already there)
                foreach (KeyValuePair<string, string> fileInfo in sourceFilesAndHashes)
                {
                    string expectedReplicaFilename = fileInfo.Key.Replace(sourcePath, replicaPath);
                    if(!(File.Exists(expectedReplicaFilename)))
                    {
                        File.Copy(fileInfo.Key, expectedReplicaFilename, false);
                        Log($"Created file {expectedReplicaFilename}", 0);
                    }

                    // Update replica files if the source ones changed
                    if(replicaFilesAndHashes.ContainsKey(expectedReplicaFilename))
                    {
                        if(sourceFilesAndHashes[fileInfo.Key] != replicaFilesAndHashes[expectedReplicaFilename])
                        {
                            try
                            {
                                File.Copy(fileInfo.Key, expectedReplicaFilename, true);
                                Log($"Overriten file {expectedReplicaFilename}", 0);
                            } catch (Exception ex)
                            {
                                // something went wrong
                                Log($"Something went wrong copying file {expectedReplicaFilename}: {ex.Message}", 1);
                            }
                        }
                    }
                }

                // Remove files that aren't in the source but are still in the replica
                foreach (KeyValuePair<string, string> fileInfo in replicaFilesAndHashes)
                {
                    string expectedSourceFilename = fileInfo.Key.Replace(replicaPath, sourcePath);
                    if(!(File.Exists(expectedSourceFilename)))
                    {
                        try {
                            File.Delete(fileInfo.Key);
                            Log($"Delete file {fileInfo.Key}", 0);
                        } catch (Exception ex)
                        {
                            Log($"Something went wrong deleting file {fileInfo.Key}: ${ex.Message}", 1);
                        } 
                  
                    }
                }

                // Remove folders that aren't in the source but are still in the replica
                foreach (string dir in replicaDirectories)
                {
                    string expectedSourceDirname = dir.Replace(replicaPath, sourcePath);
                    if(!(Directory.Exists(expectedSourceDirname))) {
                        try
                        {
                            Directory.Delete(dir);

                        } catch (Exception ex)
                        {
                            Log($"Something went wrong deleting directory {dir}: {ex.Message}", 1);
                        }
                    }
                }

                Thread.Sleep(syncInterval * 1000);
            }
        }

        static List<string> GetDirectoriesRecursive(string path)
        {
            List<string> results = new List<string>();
            if (!(Directory.Exists(path))) return results;

            List<string> subdirectories = Directory.GetDirectories(path).ToList();
            if(subdirectories.Count > 0)
            {
                results.AddRange(subdirectories);
                foreach (string dir in subdirectories)
                {
                    results.AddRange(GetDirectoriesRecursive(dir));
                }
            }

            return results;
        }

        static Dictionary<string, string> GetFilesAndHashes(string path)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            if (!(Directory.Exists(path))) return results;

            List<string> subdirectories = Directory.GetDirectories(path).ToList();
            List<string> files = Directory.GetFiles(path).ToList();

            if(files.Count > 0)
            {
                foreach (string file in files)
                {
                    try
                    {
                        results.Add(file, ComputeSHA256(file));
                    } catch (Exception ex)
                    {
                        // something wrong with accessing the file or computing its hash
                        Log($"Cannot read file: {file}: {ex.Message}", 1);
                    }
                    
                }
            }

            return results;
        }

        static string ComputeSHA256(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                using (FileStream fileStream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = sha256.ComputeHash(fileStream);
                    return BitConverter.ToString(hashBytes).Replace("-", String.Empty);
                }
            }
        }

        static void Log(string message, byte logLevel) {
            if (logFilePath == null || String.IsNullOrWhiteSpace(logFilePath)) { Console.WriteLine("Fatal Error: Failed to initialize the logger"); return; }
            if (!(Directory.Exists(Path.GetDirectoryName(logFilePath))))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
            }

            string logDatePrefix = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss ");

            string logLevelPrefix = (logLevel == 0) ? "[Info] " : (logLevel == 1) ? "[Error] " : "[Critical] ";
            ConsoleColor logLevelBackColor = (logLevel == 0) ? ConsoleColor.Black : (logLevel == 1) ? ConsoleColor.DarkRed : ConsoleColor.Red;
            ConsoleColor logLevelForeColor = (logLevel == 0) ? ConsoleColor.Gray : (logLevel == 1) ? ConsoleColor.White : ConsoleColor.Yellow;


            File.AppendAllText(logFilePath, logDatePrefix + logLevelPrefix + message + Environment.NewLine);
            Console.BackgroundColor = logLevelBackColor;
            Console.ForegroundColor = logLevelForeColor;
            Console.WriteLine(logDatePrefix + logLevelPrefix + message);

        }
    }
}
