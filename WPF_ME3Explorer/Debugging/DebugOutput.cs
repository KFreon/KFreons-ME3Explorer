using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.Debugging
{
    public class Error
    {
        public Exception exception { get; set; }
        public string ToolName { get; set; }
        public string Additional { get; set; }

        public Error(string additional, string toolname, Exception e)
        {
            Additional = additional;
            ToolName = toolname;
            exception = e;
        }
    }


    public static class DebugOutput
    {
        static DebugWindow Debugger = null;
        static MTObservableCollection<Error> AllErrors = new MTObservableCollection<Error>();
        static System.Windows.Controls.RichTextBox rtb = null;
        static DispatcherTimer UpdateTimer;
        static string DebugFilePath = null;
        static StreamWriter debugFileWriter = null;
        static StringBuilder waiting = new StringBuilder();
        static DateTime LastPrint = DateTime.Now;
        static readonly object _sync = new object();


        static DebugOutput()
        {

        }

        /// <summary>
        /// Sets textbox to output to.
        /// </summary>
        /// <param name="box">Textbox to output debug info to.</param>
        public static void SetBox(System.Windows.Controls.RichTextBox box)
        {
            try
            {
                LastPrint = DateTime.Now;
                UpdateTimer.Interval = TimeSpan.FromSeconds(0.5);
                UpdateTimer.Tick += UpdateTimer_Tick;
                UpdateTimer.Start();
                rtb = box;
            }
            catch { }
        }

        /// <summary>
        /// Checks whether the specified textbox can be written to atm.
        /// </summary>
        /// <returns>True if it can be written to.</returns>
        private static bool CheckRTB()
        {
            return rtb != null && rtb.Parent != null;
        }

        private static void UpdateTimer_Tick(object sender, EventArgs e)
        {
            lock (_sync)
            {
                if (CheckRTB())
                {

                    if (waiting.Length != 0)
                    {
                        string temp = waiting.ToString();
                        waiting.Clear();
                        rtb.AppendText(temp);

                        try
                        {
                            debugFileWriter.WriteLine(temp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine();
                        }
                    }
                }

            }
        }

        public static void PrintLn(string line = "")
        {
            PrintLn("", null);
        }

        public static void PrintLn(string line, string toolDisplayName, Exception e, params object[] bits)
        {
            if (waiting == null)
                return;

            lock (_sync)
            {
                string DTs = DateTime.Now.ToLongTimeString() + ":  " + (bits != null && bits.Length > 0 ? String.Format(line, bits) : line);

                // KFreon: Add error to list and format output accordingly
                if (e != null)
                {
                    AllErrors.Add(new Error(DTs, toolDisplayName, e));
                    waiting.AppendLine(DTs + e.Message);
                    waiting.AppendLine("-----------------------------------");
                    waiting.AppendLine(e.ToString());
                    waiting.AppendLine("-----------------------------------");
                }
                else
                {
                    // KFreon: Just print string
                    waiting.AppendLine(DTs);
                }
            }
        }

        public static void PrintLn(string s, params object[] bits)
        {
            PrintLn(s, "", null, bits);
        }

        /// <summary>
        /// Starts debugger if not already started. Prints basic info if required.
        /// </summary>
        /// <param name="toolName">Name of tool where debugging is to be started from.</param>
        public static void StartDebugger(string toolName)
        {
            if (AllErrors == null)
                AllErrors = new MTObservableCollection<Error>();

            string appender = "";
            if (rtb == null)
            {
                UpdateTimer = new DispatcherTimer();

                // KFreon: Deal with file in use
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        DebugFilePath = Path.GetDirectoryName(UsefulThings.General.GetExecutingLoc()) + "\\DebugOutput" + appender + ".txt";
                        debugFileWriter = new StreamWriter(DebugFilePath);
                        break;
                    }
                    catch
                    {
                        var t = i;
                        appender = t.ToString();
                    }
                }

                if (debugFileWriter == null)
                    PrintLn("Failed to open any debug output files. Disk cached debugging disabled for this session.");

                // KFreon: Thread debugger
                Task.Factory.StartNew(() =>
                {
                    DebugWindow debugger = new DebugWindow();
                    debugger.WindowState = System.Windows.WindowState.Minimized;
                    try
                    {
                        debugger.ShowDialog();
                    }
                    catch (NullReferenceException e)
                    {
                        // KFreon: Happens when exiting sometimes. Dunno why.
                    }
                }, TaskCreationOptions.LongRunning);

                waiting = new StringBuilder();

                // KFreon: Print basic info
                System.Threading.Thread.Sleep(200);
                PrintLn("-----New Execution of " + toolName + "-----");
                PrintLn(".........Environment Information.........");
                PrintLn("Build Version: " + UsefulThings.General.GetStartingVersion());
                PrintLn("OS Version: " + Environment.OSVersion);
                PrintLn("Architecture: " + (Environment.Is64BitOperatingSystem ? "x64 (64 bit)" : "x86 (32 bit)"));
                PrintLn("Using debug file: " + DebugFilePath);
                PrintLn(".........................................");
                PrintLn();
            }
            else
                PrintLn("-----New Execution of " + toolName + "-----");
        }
    }
}
