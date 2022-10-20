// See https://aka.ms/new-console-template for more information
using System;
using System.Dynamic;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Common;

namespace SshNet
{
    class AccessSsh
    {
        string name;
        string address;
        int port;
        string user;
        string password;
        string path;

        public AccessSsh(string inputName, string inputAddress, int inputPort, string inputUser, string inputPassword, string inputPath)
        {
            name = inputName;
            address = inputAddress;
            port = inputPort;
            user = inputUser;
            password = inputPassword;
            path = inputPath;
        }
        //Access SSH & Run SmartConnector (run.sh) 
        public void ConnectEdge()
        {
            try
            {
                //create new client & session
                using(SshClient client = new SshClient(address, port, user, password))
                {
                    client.ErrorOccurred += (sender, ex) => Console.WriteLine(ex.Exception);
                    client.Connect();

                    //create terminal -> used by ShellStream
                    //create a dictionary of terminal modes & add terminal mode
                    IDictionary<TerminalModes, uint> termkvp = new Dictionary<TerminalModes, uint>();
                    termkvp.Add(TerminalModes.ECHO, 53);

                    //execute start.sh script
                    ShellStream shellStream = client.CreateShellStream("xterm", 80, 24, 800, 600, 1024, termkvp);
                    var output = shellStream.Expect(new Regex(@"[$>]"));
                    shellStream.WriteLine($"cd {path}");
                    output = shellStream.Expect(new Regex(@"[$>]"));
                    shellStream.WriteLine("sudo su");
                    Thread.Sleep(500);
                    output = shellStream.Expect(new Regex(@"([$#>:])"));
                    shellStream.WriteLine(password);
                    shellStream.WriteLine("sh run.sh");

                    string line;
                
                    if ((line = shellStream.ReadLine()) == "sh: 0: Can't open run.sh")
                    {
                        throw new Exception("Can't open run.sh");
                    }

                    while ((line = shellStream.ReadLine(TimeSpan.FromSeconds(2))) != null)
                    {
                        Console.WriteLine(line);
                    }

                    Console.WriteLine($"{name} SmartConnector Started. Press Z to exit...");

                    while (true)
                    {
                        if (Console.KeyAvailable)
                        {
                            ConsoleKeyInfo key = Console.ReadKey();
                            if (key.Key == ConsoleKey.Z)
                            {
                                //shellStream.WriteLine($"cd {path}");
                                //output = shellStream.Expect(new Regex(@"[$>]"));
                                shellStream.WriteLine("sudo sh stop.sh");
                                Thread.Sleep(500);
                                output = shellStream.Expect(new Regex(@"([$#>:])"));
                                shellStream.WriteLine(password);

                                if(shellStream.ReadLine() == "sh: 0: Can't open stop.sh")
                                {
                                    throw new Exception("Can't open stop.sh");
                                }
                                while ((line = shellStream.ReadLine(TimeSpan.FromSeconds(2))) != null)
                                {
                                    Console.WriteLine(line);
                                }
                                Console.WriteLine($"\r\n--- {name} SmartConnector Stopped ---");
                                client.Disconnect();
                            }
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                Console.WriteLine(ex.ToString());
            }
        }
    }
    public class Program
    {
        static void Main(string[] args)
        {
            ThreadStart ts1 = new ThreadStart(ReadConfigFile);
            Thread smartConnector = new Thread(ts1);
            smartConnector.Start();
        }

        //Read edge config file
        static void ReadConfigFile()
        {
            try
            {
                Dictionary<string, string> config = File
                    .ReadLines(@".\EdgeConfigFile.txt")
                    .ToDictionary(line => line.Substring(0, line.IndexOf('=')).Trim(),
                    line => line.Substring(line.IndexOf('=') + 1).Trim());

                if (File.ReadLines(@".\EdgeConfigFile.txt") == null)
                {
                    throw new Exception("Please check EdgeConfigFile.txt");
                }

                string robotName = config["robotName"];
                string edgeAddress = config["edgeAddress"];
                int edgePort = Int32.Parse(config["edgePort"]);
                string userName = config["userName"];
                string userPw = config["userPw"]; 
                string smartConnectorPath = $"/home/{userName}/SmartConnector";

                RunSmartConnector(robotName, edgeAddress, edgePort, userName, userPw, smartConnectorPath);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        //
        static void RunSmartConnector(string robotName, string edgeAddress, int edgePort, string userName, string userPw, string smartConnectorPath)
        {
            AccessSsh robotEdge = new AccessSsh(robotName, edgeAddress, edgePort, userName, userPw, smartConnectorPath);
            robotEdge.ConnectEdge();
        }
    }
}
