﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Threading;

namespace MasterOfWebM
{
    class Helper
    {
        private const int BITCONVERSION = 8 * 1024;                         // Converts the filesize to Kilobits

        // Version check variables
        private static String downloadUrl = "";
        private static Version newVersion = null;
        private static String xmlUrl = "https://raw.githubusercontent.com/MasterOfWebM/WebM-Converter/master/update.xml";
        private static XmlTextReader reader = null;

        /// <summary>
        /// This function intakes the time format, so it can convert it to flat seconds
        /// </summary>
        /// <param name="input">A string that is formatted as HH:MM:SS</param>
        /// <returns>The seconds.</returns>
        public static double convertToSeconds(String input)
        {
            if (input.Contains("."))
            {
                input = input.Substring(0, input.LastIndexOf("."));
            }
            string[] time = input.Split(':');
            return Convert.ToDouble(time[0]) * 3600 + Convert.ToDouble(time[1]) * 60 + Convert.ToDouble(time[2]);
        }

        /// <summary>
        /// This function calculates the bitrate required to fit into a file size.
        /// </summary>
        /// <param name="size">The requested file size in MB</param>
        /// <param name="length">The length of the footage in seconds</param>
        /// <returns>The bitrate in kilobits</returns>
        public static double calcBitrate(String size, String length)
        {
            return Math.Floor(Convert.ToDouble(size) * BITCONVERSION / Convert.ToDouble(length));
        }

        /// <summary>
        /// Obtains the file size of a given file
        /// </summary>
        /// <param name="file">The file that needs to be calculated</param>
        /// <returns>The file size of a given file</returns>
        public static double getFileSize(String file)
        {
            FileInfo fi = new FileInfo(@file);

                double fileSize = fi.Length;

                return Math.Round(fileSize / 1024, 2);
            }

        /// <summary>
        /// Calls ffmpeg to encode the video
        /// </summary>
        /// <param name="command">The base command string (passes are entered automatically by this class)</param>
        /// <param name="fileOutput">The path to the output</param>
        public static void encodeVideo(String command, String fileOutput)
        {
            String commandPass1 = "-pass 1 -f webm NUL";
            String commandPass2 = "-pass 2 ";

            // Pass 1
            //Debug.WriteLine("executing pass1: ffmpeg " + command + commandPass1);
            var pass1 = Process.Start("ffmpeg", command + commandPass1);
            pass1.WaitForExit();

            // Pass 2
            //Debug.WriteLine("executing pass2: ffmpeg " + command + commandPass2 + "\"" + fileOutput + "\"");
            var pass2 = Process.Start("ffmpeg", command + commandPass2 + "\"" + fileOutput + "\"");
            pass2.WaitForExit();
        }

        /// <summary>
        /// Checks to see if ffmpeg has a font.conf installed, and if it doesn't
        /// it will install one for the user to support subtitles
        /// </summary>
        /// <returns>If the current FFmpeg installation has a font config installed</returns>
        public static bool checkFFmpegFontConfig()
        {
            // Spawn process to check if ffmpeg is installed and find out where it is
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "cmd";
            p.StartInfo.Arguments = "/k where ffmpeg & exit";
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
           
            p.WaitForExit();

            if (output == "")
            {
                MessageBox.Show("FFmpeg is not installed, please either put it in the same directory\n"+
                                "as this program or in your 'PATH' Environment Variable.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);

                return false;
            }
            else
            {
                // Get rid of the newline at the end of the output
                if (output.Contains(Environment.NewLine))
                {
                    output = output.Substring(0, output.IndexOf(Environment.NewLine));
                }
                output = output.Replace(Environment.NewLine, "");

                // Get the root directory of ffmpeg
                output = Path.GetDirectoryName(@output);

                if (File.Exists(output + "\\fonts\\fonts.conf"))
                {
                    return true;
                }
                else
                {
                    // MessageBox.Show("missing file: " + output + "\\fonts\\fonts.conf");
                    if (Directory.Exists(output + "\\fonts"))
                    {
                        // If the directory actually exists, just write the config file
                        try
                        {
                            File.Copy("fonts\\fonts.conf", output + "\\fonts\\fonts.conf");

                            return true;
                        }
                        catch (Exception ex)
                        {
                            if (ex is UnauthorizedAccessException)
                            {
                                MessageBox.Show("Failed to create the fonts config due to\n" +
                                    "Unathorized Access. Please start this program with Administrator\n" +
                                    "privileges.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                                return false;
                            }
                            else
                            {
                                MessageBox.Show("Something went wrong with writing the config\n" +
                                "file. Please message Master Of WebM to figure it out.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                                return false;
                            }
                        }
                    }
                    else
                    {
                        // If neither the directory, or file exists, then create them both
                        try
                        {
                            Directory.CreateDirectory(output + "\\fonts");
                            File.Copy("fonts\\fonts.conf", output + "\\fonts\\fonts.conf");

                            return true;
                        }
                        catch (Exception ex)
                        {
                            if (ex is UnauthorizedAccessException)
                            {
                                MessageBox.Show("Failed to create the fonts config due to\n" +
                                    "Unathorized Access. Please start this program with Administrator\n" +
                                    "privileges.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                                return false;
                            }
                            else
                            {
                                MessageBox.Show("Something went wrong with writing the config\n" +
                                "file. Please message Master Of WebM to figure it out.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                                return false;
                            }
                        }
                    }
                }
            }
        }

        public static void checkUpdateInNewThread()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = false;
                checkUpdate();
            }).Start();
        }

        /// <summary>
        /// Verifies the version of the program.
        /// It will prompt the user if the program is
        /// outdated.
        /// </summary>
        public static void checkUpdate()
        {
            try
            {
                reader = new XmlTextReader(xmlUrl);
                reader.MoveToContent();
                string elementName = "";

                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "webmconverter"))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            elementName = reader.Name;
                        }
                        else
                        {
                            if ((reader.NodeType == XmlNodeType.Text) && (reader.HasValue))
                            {
                                switch (elementName)
                                {
                                    case "version":
                                        newVersion = new Version(reader.Value);
                                        break;
                                    case "url":
                                        downloadUrl = reader.Value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Error out to not disrupt the user
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }

            // Current version of the application
            Version appVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (appVersion.CompareTo(newVersion) < 0)
            {
                if (MessageBox.Show("You are currently out of date.\nWould you like to update now? Remember to apply your github changes! :)", "Version out of date", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    var update = Process.Start(downloadUrl);
                }
            }
        }

        /// <summary>
        /// Checks if a subtitle (subs.ass | subs.srt) exists
        /// and deletes it.
        /// </summary>
        public static void subsCheck()
        {
            if(File.Exists("subs.ass"))
            {
                File.Delete("subs.ass");
            }
            else if (File.Exists("subs.srt"))
            {
                File.Delete("subs.srt");
            }
        }

        /// <summary>
        /// Converts input to HH:MM:SS format.
        ///        1   -> 00:00:01
        ///       11   -> 00:00:11
        ///     1:11   -> 00:01:11
        ///    11:11   -> 00:11:11
        ///  1:11:11   -> 01:11:11
        /// 11:11:11   -> 11:11:11
        ///       11.5 -> 00:00:11.5
        ///    11:11.3 -> 00:11:11.3
        /// </summary>
        /// <param name="timeInput"></param>
        /// <returns></returns>
        public static string fillMissingZeroes(string timeInput)
        {
            string millis = "";
            if (timeInput.Contains("."))
            {
                millis = getMillisFromTimeStart(timeInput);
                timeInput = getHHMMSSFromTimeStart(timeInput);
            }
            string zeroes = "00:00:00";
            return zeroes.Substring(0, zeroes.Length - timeInput.Length) + timeInput + millis;
        }

        /// <summary>
        /// Gets ".Y" part from "XX:XX:XX.Y"
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string getMillisFromTimeStart(string text)
        {
            if (text.Contains("."))
            {
                return text.Substring(text.IndexOf("."));
            }
            return "";
        }

        /// <summary>
        /// Gets "XX:XX:XX" part from "XX:XX:XX.Y"
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string getHHMMSSFromTimeStart(string text)
        {
            if (text.Contains("."))
            {
                text = text.Substring(0, text.LastIndexOf("."));
            }
            return text;
        }
    }
}
