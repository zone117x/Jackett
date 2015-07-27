﻿using CommandLine;

namespace Jackett.Console
{
    public class ConsoleOptions
    {
        [Option('i', "Install", HelpText = "Install Jackett windows service (Must be admin)")]
        public bool Install { get; set; }

        [Option('r', "ReserveUrls",  HelpText = "(Re)Register windows port reservations (Required for listening on all interfaces).")]
        public bool ReserveUrls { get; set; }

        [Option('u', "Uninstall", HelpText = "Uninstall Jackett windows service (Must be admin).")]
        public bool Uninstall { get; set; }

        [Option('l', "Logging",  HelpText = "Log all requests/responses to Jackett")]
        public bool Logging { get; set; }

        [Option('t', "Tracing", HelpText = "Enable tracing")]
        public bool Tracing { get; set; }

        [Option('c', "UseCurlExec",  HelpText = "Execute curl rather than libcurl for all outgoing requests.")]
        public bool UseCurlExec { get; set; }

        [Option('s', "Start",  HelpText = "Start the Jacket Windows service (Must be admin)")]
        public bool StartService { get; set; }

        [Option('k', "Stop", HelpText = "Stop the Jacket Windows service (Must be admin)")]
        public bool StopService { get; set; }

        [Option('x', "ListenPublic", HelpText = "Listen publicly")]
        public bool? ListenPublic { get; set; }

        [Option('h', "Help",  HelpText = "Show Help")]
        public bool ShowHelp { get; set; }

        [Option('v', "Version",  HelpText = "Show Version")]
        public bool ShowVersion { get; set; }

        [Option('p', "Port", HelpText = "Web server port")]
        public int Port { get; set; }

        [Option('m', "MigrateSettings", HelpText = "Migrate settings manually (Must be admin on Windows)")]
        public bool MigrateSettings { get; set; }
    }
}
