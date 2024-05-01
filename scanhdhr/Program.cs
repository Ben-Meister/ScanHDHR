using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using CommandLine;

/*
 * Sample of what you obtain from the HDHR's internal webserver:
 * =============================================================
 * 
Virtual Channel none
Frequency       57.000 MHz
Program Number  none
Authorization   none
CCI Protection  none
Modulation Lock qam256
PCR Lock        locked
Signal Strength 100% (0.4 dBmV)
Signal Quality  100% (35.2 dB)
Symbol Quality  100%
Streaming Rate  none
Resource Lock   none
 */

namespace scanhdhr
{
    public class Config
    {
        // The IP address of the HDHR unit
        public string IP;
        // The path to hdhomerun_config.exe
        public string HDHRExePath;
        // The number of the tuner to use
        public int tuner;
        // True if this is an Antenna HDHR; otherwise False (Cable)
        public bool antenna;
        // True if quickscan mode; otherwise False (fullscan mode)
        public bool quickscan;
        // The directory in which to write the output log files
        public string logpath;
        // For quickscan mode, the file name in which to write the single quickscan result (for PRTG)
        public string quickscansingleresultfilename;
        // For full scan mode, the starting channel
        public int minchannel;
        // For full scan mode, the ending channel
        public int maxchannel;
        // The delay to wait after tuning a channel before reading the signal, in msec
        public int channeltunedelay;
        // For quickscan mode, offsets to apply to the read channel signal strength values
        public Dictionary<int, double> offsets = new Dictionary<int, double>();
        // For quickscan mode, which channels to scan
        public List<int> qschannelstoscan = new List<int>();

        public void LoadSampleValues()
        {
            IP = "192.168.3.5";
            HDHRExePath = @"C:\Program Files\Silicondust\HDHomeRun\hdhomerun_config.exe";
            tuner = 1;
            logpath = @"C:\scanlogs";
            quickscansingleresultfilename = @"c:\inetpub\wwwroot\qsresult.txt";
            quickscan = false;
            minchannel = 2;
            maxchannel = 157; //157 for full scan, 107 for reduced
            channeltunedelay = 3000; //was 3000, then had 1500
            antenna = false;

            // Calibration offsets
            // Compare the HDHR against a known good meter, then define a series of offsets to
            // compensate for the fact that the HDHR is uncalibrated
            offsets = new Dictionary<int, double>();
            offsets.Add(2, 5);
            offsets.Add(6, 2.5);
            offsets.Add(98, 2.9);
            offsets.Add(11, 0.9);
            offsets.Add(37, 3.2);
            offsets.Add(72, 4.2);
            offsets.Add(87, 4.2);
            offsets.Add(107, 4.4);
            offsets.Add(123, 4.4); //Guesstimate, need to verify with trilithic!
            offsets.Add(157, 4.4); //Guesstimate, need to verify with trilithic!

            qschannelstoscan = new List<int>();
            qschannelstoscan.Add(123);//2);
            qschannelstoscan.Add(157);//6);
            qschannelstoscan.Add(98);
            qschannelstoscan.Add(11);
            qschannelstoscan.Add(37);
            qschannelstoscan.Add(72);
            qschannelstoscan.Add(87);
            qschannelstoscan.Add(107);
        }

        public void WriteToJsonFile(string path)
        {
            // serialize JSON directly to a file
            using (StreamWriter file = File.CreateText(path))
            {
                new JsonSerializer().Serialize(file, this);
            }
        }

        public string GetJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
    class Program
    {
        // Execute Command Line Process and return the output as a string
        static string RunCommand(string exepath, string args)
        {
            Process myProcess = new Process();
            string s;
            myProcess.StartInfo.FileName = exepath;
            myProcess.StartInfo.Arguments = args;
            myProcess.StartInfo.UseShellExecute = false;
            myProcess.StartInfo.CreateNoWindow = true; 
            myProcess.StartInfo.RedirectStandardInput = true; 
            myProcess.StartInfo.RedirectStandardOutput = true;
            myProcess.StartInfo.RedirectStandardError = true;
            myProcess.Start();

            StreamWriter sIn = myProcess.StandardInput; 
            StreamReader sOut = myProcess.StandardOutput; 
            StreamReader sErr = myProcess.StandardError;
 
            sIn.AutoFlush = true; 
            s = sOut.ReadToEnd();

            sIn.Close();
            sOut.Close(); 
            sErr.Close() ;
            myProcess.Close(); 
            return s;
        }

        class Options
        {
            [Option('c', "configfile", Required = true, HelpText = "Path to the JSON config file.")]
            public string configfile { get; set; }

            [Option('q', "quick",
              Default = false,
              HelpText = "Perform a quick scan (default: full)")]
            public bool quickscan { get; set; }

            [Option('p', "printconfig",
  Default = false,
  HelpText = "Print config parameters to the screen (as JSON)")]
            public bool printconfig { get; set; }

            [Option("writesample",
              Default = false,
              HelpText = "Write a sample config file to the location provided.")]
            public bool writesample { get; set; }

        }

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
              .WithParsed(RunOptions)
              .WithNotParsed(HandleParseError);
        }
        static void RunOptions(Options opts)
        {
            Config c = new Config();
            if (opts.writesample)
            {
                try
                {
                    // Write a sample config file to the location provided
                    c.LoadSampleValues();
                    c.WriteToJsonFile(opts.configfile);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to process the JSON file! " + e.Message);
                }
            }
            else
            {
                try
                {
                    // Load the config
                    using (StreamReader file = File.OpenText(opts.configfile))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        c = (Config)serializer.Deserialize(file, typeof(Config));
                    }
                    if (opts.quickscan) c.quickscan = true;
                    if (opts.printconfig) Console.WriteLine(c.GetJsonString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to process the JSON file! " + e.Message);
                }
                try
                {
                    DoProcess(c);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to complete the scan! " + e.Message);
                }
            }
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            return;
        }

        static void DoProcess(Config c) { 
            string filename =  c.logpath + @"\" + DateTime.Now.ToString("s").Replace(":", ".") + ".csv";
            string quickscanfilename = c.logpath + @"\quickscanOFFSET.csv";
            string quickscanstr = "";
            WebClient wb = new WebClient();
            List<int> channelstoscan = new List<int>();

            // Antenna units have a lower max channel and do not give the dBmV/dB readings, so we have to compensate
            string[] signalcolumns = { "Signal Strength (dBmV)", "SNR (dB)" };
            string shortheader = "Ch\tFrequency\tRaw Strength (dBmV)\tSNR (dB)\tSymQ";
            if (c.antenna)
            {
                c.maxchannel = Math.Min(c.maxchannel, 68);
                signalcolumns[0] = "Signal Strength";
                signalcolumns[1] = "Signal Quality";
                shortheader = "Ch\tFrequency\tStrength\tSigQ\tSymQ";
            }
            string longheader = "Ch,Frequency," + signalcolumns[0] + "," + signalcolumns[1] + ",Symbol Quality,Program,Virtual Channel,Callsign,Flags\r\n";

            // END LOCAL VARS

            // See if the tuner is in use. If so, abort.
            string si = RunCommand(c.HDHRExePath, c.IP + " get /tuner" + c.tuner + "/channel");
            if (!(si.Contains("none")))
            {
                Console.WriteLine("Tuner in use, aborting!");
                return;
            }

            // Prepare channel list, file headers for scanning
            if (!c.quickscan)
            {
                for (int i = c.minchannel; i <= c.maxchannel; i++)   
                {
                    channelstoscan.Add(i);
                }
                // We only write the header in full scan mode
                File.AppendAllText(filename, longheader);
            }
            else
            {
                channelstoscan = c.qschannelstoscan;
            }

            Console.WriteLine(shortheader);
            foreach (int i in channelstoscan)
            {
                // Set the channel, and wait for it to be tuned.
                RunCommand(c.HDHRExePath, c.IP + " set /tuner" + c.tuner + "/channel " + i);
                System.Threading.Thread.Sleep(c.channeltunedelay); 

                // Retrieve the dBmV data from the webserver (not available from hdhr config exe)
                string indata = wb.DownloadString("http://"+ c.IP + "/tuners.html?page=tuner" + c.tuner);

                // Parse the data from the HTML
                string[] indatas = indata
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => x.Contains("<tr>"))
                    .Select(x => x.Replace("<tr><td>", "").Replace("</td></tr>", "").Replace("</td><td>", "\t"))
                    .ToArray();
                Dictionary<string, string> response = new Dictionary<string, string>();
                foreach (string x in indatas)
                {
                    string[] kvpair = x.Split('\t');
                    response.Add(kvpair[0], kvpair[1]);
                }

                // Format the data from the HTML, and break out the dBmV values
                response["Frequency"] = response["Frequency"].Replace(" MHz", "");
                response.Add("Signal Strength (dBmV)", response["Signal Strength"].Contains("dBmV") ?
                    response["Signal Strength"].Replace("(", "").Split(' ')[1] :
                    "none");
                response.Add("SNR (dB)", response["Signal Quality"].Contains("dB") ?
                    response["Signal Quality"].Replace("(", "").Split(' ')[1] :
                    "none");
                
                // Display summary results on screen for user progress
                Console.WriteLine(String.Join("\t", i, response["Frequency"], response[signalcolumns[0]], response[signalcolumns[1]], response["Symbol Quality"]));
                if (!c.quickscan)
                {
                    // Since it is a full scan, get the programs on each channel (Program,Virtual Channel,Callsign,Flags)
                    si = RunCommand(c.HDHRExePath, c.IP + " get /tuner" + c.tuner + "/streaminfo");
                    if (si.Length > 10)
                    {
                        // Write a line in the CSV for each PROGRAM (channel will appear multiple times)
                        string[] splitlines = si.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (string s in splitlines)
                        {
                            if (s.Length > 2)
                            {
                                string x = s.Replace(":", "").Replace(" ", ",");
                                File.AppendAllText(filename, String.Join(",", i, response["Frequency"], response[signalcolumns[0]], response[signalcolumns[1]], response["Symbol Quality"], x) + "\r\n");
                            }
                        }
                    }
                    else
                    {
                        // Write one line to represent the channel in the CSV, no program information
                        File.AppendAllText(filename, String.Join(",", i, response["Frequency"], response[signalcolumns[0]], response[signalcolumns[1]], response["Symbol Quality"]) + "\r\n");
                    }
                }
                else
                {
                    // For the quickscan mode, we don't write a line for each channel. Instead we buffer the data because we write one line for each scan.
                    try {
                        double offset = 0;
                        if (c.offsets.ContainsKey(i)) offset = c.offsets[i];
                        quickscanstr += String.Join(",", double.Parse(response[signalcolumns[0]]) + offset, response[signalcolumns[1]]) + ",";
                    } catch(Exception e) {}
                }
            }

            // We're finished scanning. Append the quickscan logs to the running logfile to show results over time
            // Write the single result to a separate file for consumption by other programs (PRTG)
            if (c.quickscan)
            {
                File.AppendAllText(quickscanfilename, DateTime.Now.ToString("s") + "," + quickscanstr + "\r\n");
                File.WriteAllText(c.quickscansingleresultfilename,"[" + quickscanstr.Replace("none","0").Replace(",","][") + "]" + "\r\n");
            }

            // Last, free the tuner
            RunCommand(c.HDHRExePath, c.IP + " set /tuner" + c.tuner + "/channel none");
        }
    }
}
