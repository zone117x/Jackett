using Jackett.Indexers;
using CommandLine;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Jackett
{
    class Program
    {
        public static string AppConfigDirectory { get; private set; }

        public static int Port { get; private set; }

        public static bool ListenPublic { get; private set; }

        public static bool ListenSet { get; private set; }

        public static Server ServerInstance { get; private set; }

        public static bool IsFirstRun { get; private set; }

        public static Logger LoggerInstance { get; private set; }

        public static ManualResetEvent ExitEvent { get; private set; }

        public static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }



        static void Main(string[] args)
        {
            ExitEvent = new ManualResetEvent(false);
            var result = CommandLine.Parser.Default.ParseArguments<Options>(args);
            var exitCode = result
                    .Return(
                               options =>
                {
                    if (options.Verbose)
                    {
                        Console.WriteLine("Directory: {0}, port: {1}, listen public: {2}, listen private: {3}, listen set: {4}", 
                            options.Directory, options.Port, 
                            options.ListenPublic, options.ListenPrivate, 
                            options.ListenSet);
                    }
                    Perform(options);
                    return 0;
                },
                               errors =>
                {
                    return 1;
                });

        }

        static void Perform(Options options)
        {
            if (!(options.Directory == null))
            {
                AppConfigDirectory = options.Directory;
            }
            else
            {
                AppConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jackett");
            }
            Port = options.Port;
            Console.WriteLine("options.ListenSet: " + options.ListenSet);
            if (options.ListenSet)
            {
                ListenSet = true;
                if (options.ListenPublic)
                    ListenPublic = true;
                else
                    ListenPublic = false;
            }
            MigrateSettingsDirectory();

            try
            {
                if (!Directory.Exists(AppConfigDirectory))
                {
                    IsFirstRun = true;
                    Directory.CreateDirectory(AppConfigDirectory);
                }
                Console.WriteLine("App config/log directory: " + AppConfigDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create settings directory. " + ex.Message);
                Application.Exit();
                return;
            }

            var logConfig = new LoggingConfiguration();

            var logFile = new FileTarget();
            logConfig.AddTarget("file", logFile);
            logFile.Layout = "${longdate} ${level} ${message} \n ${exception:format=ToString}\n";
            logFile.FileName = Path.Combine(AppConfigDirectory, "log.txt");
            logFile.ArchiveFileName = "log.{#####}.txt";
            logFile.ArchiveAboveSize = 500000;
            logFile.MaxArchiveFiles = 1;
            logFile.KeepFileOpen = false;
            logFile.ArchiveNumbering = ArchiveNumberingMode.DateAndSequence;
            var logFileRule = new LoggingRule("*", LogLevel.Debug, logFile);
            logConfig.LoggingRules.Add(logFileRule);

            if (Program.IsWindows)
            {
#if !__MonoCS__
                var logAlert = new MessageBoxTarget();
                logConfig.AddTarget("alert", logAlert);
                logAlert.Layout = "${message}";
                logAlert.Caption = "Alert";
                var logAlertRule = new LoggingRule("*", LogLevel.Fatal, logAlert);
                logConfig.LoggingRules.Add(logAlertRule);
#endif
            }

            var logConsole = new ConsoleTarget();
            logConfig.AddTarget("console", logConsole);
            logConsole.Layout = "${longdate} ${level} ${message} ${exception:format=ToString}";
            var logConsoleRule = new LoggingRule("*", LogLevel.Debug, logConsole);
            logConfig.LoggingRules.Add(logConsoleRule);

            LogManager.Configuration = logConfig;
            LoggerInstance = LogManager.GetCurrentClassLogger();

            UpdateSettingsFile();
            ReadSettingsFile();

            var serverTask = Task.Run(async () =>
            {
                ServerInstance = new Server(Port, ListenPublic);
                await ServerInstance.Start();
            });

            try
            {
                if (Program.IsWindows)
                {
#if !__MonoCS__
                    Application.Run(new Main());
#endif
                }
            }
            catch (Exception)
            {

            }

            Console.WriteLine("Running in headless mode.");



            Task.WaitAll(serverTask);
            Console.WriteLine("Server thread exit");
        }

        public static void RestartServer()
        {

            ServerInstance.Stop();
            ServerInstance = null;
            ReadSettingsFile();
            var serverTask = Task.Run(async () =>
            {
                ServerInstance = new Server(Port, ListenPublic);
                await ServerInstance.Start();
            });
            Task.WaitAll(serverTask);
        }

        static void MigrateSettingsDirectory()
        {
            try
            {
                string oldDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Jackett");
                if (Directory.Exists(oldDir) && !Directory.Exists(AppConfigDirectory))
                {
                    Directory.Move(oldDir, AppConfigDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR could not migrate settings directory " + ex);
            }
        }

        static void UpdateSettingsFile()
        {
            var path = Path.Combine(AppConfigDirectory, "config.json");
            JObject f = new JObject();
            if (File.Exists(path) && (!(Port == 0) || ListenSet))
            {
                var configJson = JObject.Parse(File.ReadAllText(path));
                if (!(Port == 0))
                    f.Add("port", Port);
                else
                    f.Add("port", (int)configJson.GetValue("port"));
                if (ListenSet)
                    f.Add("public", ListenPublic);
                else
                    f.Add("public", (bool)configJson.GetValue("public"));
                File.WriteAllText(path, f.ToString());
            }
            else
            {
                if (Port == 0)
                {
                    Console.WriteLine("putting default port");
                    f.Add("port", Server.DefaultPort);
                }
                else
                {
                    Console.WriteLine("putting port {0}", Port);
                    f.Add("port", Port);
                }
                if (ListenSet)
                    f.Add("public", ListenPublic);
                else
                    f.Add("public", true);         
                File.WriteAllText(path, f.ToString());
            }
        }
        
        static void ReadSettingsFile()
        {
            var path = Path.Combine(AppConfigDirectory, "config.json");
            var configJson = JObject.Parse(File.ReadAllText(path));
            Port = (int)configJson.GetValue("port");
            ListenPublic = (bool)configJson.GetValue("public");

            Console.WriteLine("Config file path: " + path);
        }

        static public void RestartAsAdmin()
        {
            var startInfo = new ProcessStartInfo(Application.ExecutablePath.ToString()) { Verb = "runas" };
            Process.Start(startInfo);
            Environment.Exit(0);
        }
    }
}
