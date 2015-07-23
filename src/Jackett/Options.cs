using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jackett
{
    public class Options
    {
        public bool ListenSet = false;
        private bool listenPublic;
        private bool listenPrivate;

        [Option('d', "directory", 
            HelpText = "Configuration directory, any other command line arguments will override the configuration")]
        public string Directory { get; set; }

        [Option('p', "port", HelpText = "Port to use")]
        public int Port { get; set; }

        [Option('l', "listen_public", HelpText = "Listen publicly")]
        public bool ListenPublic
        {
            get { return listenPublic; }
            set
            {
                ListenSet = true; 
                listenPublic = true;
            }
        }

        [Option('L', "listen_private", HelpText = "Listen privately")]
        public bool ListenPrivate
        {
            get{ return listenPrivate; }
            set
            {
                ListenSet = true;
                listenPrivate = true;
            }
        }

        [Option(
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
    }
}
