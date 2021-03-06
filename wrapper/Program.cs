﻿namespace wrapper
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class Program
    {
        public static void Main(string[] args)
        {
            var executable = ConfigurationManager.AppSettings["executable"];
            string newArgs = null;
            if (!string.IsNullOrWhiteSpace(executable))
            {
                newArgs = string.Join(" ", args);
            }

            var outputTrace = new List<string>();
            var infoTrace = new List<string>();

            int? processExitCode = null;
            if (newArgs != null)
            {
                var process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.FileName = executable;
                process.StartInfo.Arguments = newArgs;

                Regex outputLineRegex = null;
                var outputLinePattern = ConfigurationManager.AppSettings["outputLinePattern"];
                var outputLineReplacement = ConfigurationManager.AppSettings["outputLineReplacement"];
                if (!string.IsNullOrWhiteSpace(outputLinePattern) && outputLineReplacement != null)
                {
                    outputLineRegex = new Regex(outputLinePattern, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
                }

                process.Start();
                string line;
                do
                {
                    line = process.StandardOutput.ReadLine();
                    if (line != null)
                    {
                        if (outputLineRegex != null && outputLineRegex.IsMatch(line))
                        {
                            var replecedLine = outputLineRegex.Replace(line, outputLineReplacement);
                            Console.WriteLine(replecedLine);
                            outputTrace.Add("!!! Replaced line !!!");
                            outputTrace.Add(line);
                            outputTrace.Add("!!! New line !!! ");
                            outputTrace.Add(replecedLine);
                        }
                        else
                        {
                            Console.WriteLine(line);
                            outputTrace.Add(line);
                        }
                    }
                }
                while (line != null);
                process.WaitForExit();
                processExitCode = process.ExitCode;
            }

            int overridedExitCode;
            if (int.TryParse(ConfigurationManager.AppSettings["overridedExitCode"], out overridedExitCode))
            {
                if (processExitCode != null)
                {
                    infoTrace.Add($"!!! Override exit code {processExitCode} by {overridedExitCode}!!!");
                }

                processExitCode = overridedExitCode;
            }

            var exitCode = (processExitCode ?? 0);
            var traceCommandLineFile = GetTraceFile(ConfigurationManager.AppSettings["traceCommandLineFile"]);
            if (!string.IsNullOrWhiteSpace(traceCommandLineFile))
            {
                using (var traceCommandLine = File.CreateText(traceCommandLineFile))
                {
                    traceCommandLine.WriteLine($"@pushd {Environment.CurrentDirectory}");
                    traceCommandLine.WriteLine();
                    foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
                    {
                        traceCommandLine.WriteLine($"@SET \"{environmentVariable.Key}={environmentVariable.Value}\"");
                    }

                    traceCommandLine.WriteLine();
                    traceCommandLine.WriteLine(Environment.CommandLine);
                    traceCommandLine.WriteLine();

                    if (newArgs != null)
                    {
                        traceCommandLine.WriteLine($"@REM {executable} {newArgs}");
                        traceCommandLine.WriteLine();
                    }

                    traceCommandLine.WriteLine("@popd");

                    traceCommandLine.WriteLine();
                    traceCommandLine.WriteLine($"@REM Configuration.executable: {ConfigurationManager.AppSettings["executable"]}");
                    traceCommandLine.WriteLine($"@REM Configuration.overridedExitCode: {ConfigurationManager.AppSettings["overridedExitCode"]}");
                    traceCommandLine.WriteLine($"@REM Configuration.traceCommandLineFile: {ConfigurationManager.AppSettings["traceCommandLineFile"]}");
                    traceCommandLine.WriteLine($"@REM Configuration.outputLinePattern: {ConfigurationManager.AppSettings["outputLinePattern"]}");
                    traceCommandLine.WriteLine($"@REM Configuration.outputLineReplacement: {ConfigurationManager.AppSettings["outputLineReplacement"]}");
                    traceCommandLine.WriteLine($"@REM Is64BitProcess: {Environment.Is64BitProcess}");
                    traceCommandLine.WriteLine($"@REM Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
                    traceCommandLine.WriteLine($"@REM OSVersion: {Environment.OSVersion}");
                    traceCommandLine.WriteLine($"@REM ProcessorCount: {Environment.ProcessorCount}");
                    traceCommandLine.WriteLine($"@REM AppDomain BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
                    traceCommandLine.WriteLine($"@REM AppDomain ConfigurationFile: {AppDomain.CurrentDomain.SetupInformation.ConfigurationFile}");
                    foreach (var infoLine in infoTrace)
                    {
                        traceCommandLine.WriteLine($"@REM {infoLine}");
                    }
                    traceCommandLine.WriteLine($"@REM Exit code: {exitCode}");
                    traceCommandLine.WriteLine();
                    traceCommandLine.WriteLine("@REM Output:");
                    foreach (var outputTraceLine in outputTrace)
                    {
                        traceCommandLine.WriteLine($"@REM {outputTraceLine}");
                    }

                    traceCommandLine.Flush();
                }
            }

            Environment.Exit(exitCode);
        }

        private static string GetTraceFile(string traceCommandLineFile)
        {
            if (string.IsNullOrWhiteSpace(traceCommandLineFile))
            {
                return traceCommandLineFile;
            }

            var fileName = Path.GetFileName(traceCommandLineFile);
            var dirName = Path.GetDirectoryName(traceCommandLineFile) ?? string.Empty;
            if (string.IsNullOrEmpty(dirName))
            {
                dirName = Environment.CurrentDirectory;
            }

            var fileRegex = new Regex($"(\\d+)\\.{fileName}", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var numVal = 0;
            var filesNums = (
                from file in Directory.GetFiles(dirName)
                let curFileName = Path.GetFileName(file)
                let match = fileRegex.Match(curFileName)
                where match.Success && match.Groups.Count == 2 && int.TryParse(match.Groups[1].Value, out numVal)
                let num = numVal
                select numVal).ToArray();

            int nextNum = 0;
            if (filesNums.Any())
            {
                nextNum = filesNums.Max() + 1;
            }

            return Path.Combine(dirName, $"{nextNum:00000}.{fileName}");
        }
    }
}
