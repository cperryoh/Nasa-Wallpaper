using Newtonsoft.Json;
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Nasa_Wallpaper_framework_app
{
    class JSON
    {
        public String date { get; set; }
        public String explanation { get; set; }
        public String hdurl { get; set; }
    }
    [Serializable]
    class ProgramData
    {
        public bool bootOnStart;
        public String url = "";
        public String api = "";
    }
    class Program
    {
        static Thread updateThread;
        static ProgramData info;

        //file store and load functions
        static String loadData(String currentUrl)
        {
            if (info == null)
            {
                info = new ProgramData();
            }
            try
            {
                IFormatter formatter = new BinaryFormatter();
                Stream stream = new FileStream(curDir + "data.bin", FileMode.Open, FileAccess.Read, FileShare.Read);
                info = (ProgramData)formatter.Deserialize(stream);
                stream.Close();
            }
            catch (Exception e)
            {
                info.url = currentUrl;
            }
            return info.url;
        }
        static void storeData(String url)
        {
            info.url = url;
            try
            {
                IFormatter formatter = new BinaryFormatter();
                Stream stream = new FileStream(curDir + "data.bin", FileMode.Create, FileAccess.Write, FileShare.None);
                formatter.Serialize(stream, info);
                stream.Close();
            }
            catch (Exception e)
            {
                Log(LogType.ERROR, "Unable to write new image url to text file\n" + e.Message);
            }
        }

        //log enum and functions
        enum LogType { LOG, ERROR }
        static void Log(LogType type, String message)
        {
            StreamWriter log = File.CreateText(curDir + "log.txt");
            string txt = DateTime.Now.ToString("[dd/MM/yyyy-hh:mm:ss]") + $"{type}: {message}";
            log.WriteLine(txt);
            Console.WriteLine(txt);
            log.Close();
        }


        //api functions
        static String getCurUrl()
        {
            try
            {
                System.Net.WebClient wc = new System.Net.WebClient();
                string webData = wc.DownloadString("https://" + $"api.nasa.gov/planetary/apod?api_key={info.api}");
                JSON obj = JsonConvert.DeserializeObject<JSON>(webData);
                return obj.hdurl;
            }
            catch (Exception e)
            {
                Log(LogType.ERROR, "Unable to get image url from api\n" + e.Message);

                return null;
            }
        }
        static void getImage(String url)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(url, "images\\" + DateTime.Now.ToString("MM-dd-yyyy") + ".jpg");
                }
            }
            catch (Exception e)
            {
                Log(LogType.ERROR, "Unable to retrive image from url\n" + e.Message);
            }
        }


        //ui functions
        static ToolStripMenuItem startOnBoot;
        static void makeTrayIcon()
        {
            NotifyIcon trayIcon = new NotifyIcon();
            trayIcon.Text = "Nasa picture of the day";
            trayIcon.Icon = Nasa_Wallpaper_Core.Properties.Resources.icon;

            ContextMenuStrip menu = new ContextMenuStrip();
            trayIcon.ContextMenuStrip = menu;

            ToolStripMenuItem openImagesFolder = new ToolStripMenuItem
            {
                Text = "Open Images Folder"
            };
            openImagesFolder.Click += new EventHandler(openFolder);
            menu.Items.Add(openImagesFolder);


            ToolStripMenuItem chgApiKey = new ToolStripMenuItem
            {
                Text = "Change Api Key"
            };
            chgApiKey.Click += new EventHandler(changeApiKey);
            menu.Items.Add(chgApiKey);


            startOnBoot = new ToolStripMenuItem
            {
                Text = "Start app on boot"
            };
            startOnBoot.Click += new EventHandler(check);
            startOnBoot.Checked = info.bootOnStart;
            menu.Items.Add(startOnBoot);


            ToolStripMenuItem quitItem = new ToolStripMenuItem
            {
                Text = "Quit"
            };
            quitItem.Click += new EventHandler(quit);
            menu.Items.Add(quitItem);

            trayIcon.Visible = true;
            Application.Run();
        }

        private static void openFolder(object sender, EventArgs e)
        {
            Process.Start(curDir + "images\\");
        }

        static void getApiKey()
        {
            ApiKeyForm form = new ApiKeyForm();
            form.ShowDialog();
            info.api = form.api;
            storeData(info.url);
        }

        //action functions 
        private static void changeApiKey(object sender, EventArgs e)
        {
            getApiKey();
        }

        static void check(object sender, EventArgs e)
        {
            startOnBoot.Checked = !startOnBoot.Checked;
            SetStartup(startOnBoot.Checked);
        }
        private static void quit(object sender, EventArgs e)
        {
            updateThread.Interrupt();
            Environment.Exit(0);
        }

        //set startup func
        static void SetStartup(bool start)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            String name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            if (start)
                rk.SetValue(name, Application.ExecutablePath);
            else
                rk.DeleteValue(name, false);
        }


        //file functions
        public static void makeImagesFolder()
        {
            bool fileExists = Directory.Exists(curDir + "images");
            if (!fileExists)
            {
                Directory.CreateDirectory(curDir + "images");
            }
        }
        public static void cleanUpImagesFolder()
        {
            int fCount = Directory.GetFiles(curDir + "images", "*", SearchOption.TopDirectoryOnly).Length;
            if (fCount > 30)
            {
                FileSystemInfo fileInfo = new DirectoryInfo(curDir + "images").GetFileSystemInfos().OrderBy(fi => fi.CreationTime).First();
                Directory.Delete(fileInfo.FullName);
            }
        }

        //worker thread
        static String curDir = System.AppContext.BaseDirectory;
        public static void updateWallpaper()
        {
            //calc delay time in ms
            int miliseconds = 1 * 60 * 60 * 100;

            //query api for url
            String curUrl = getCurUrl();
            storeData(curUrl);

            //setup images folder
            makeImagesFolder();

            //get image from url
            getImage(curUrl);

            //display image from file
            DisplayPicture(curDir + "images\\" + DateTime.Now.ToString("MM-dd-yyyy") + ".jpg");

            //log that intial startup is done
            Log(LogType.LOG, "Booted application");
            while (true)
            {
                //delay
                Thread.Sleep(miliseconds);

                //log
                Log(LogType.LOG, "Updating app");

                //load new url from api
                curUrl = getCurUrl();

                //compare new url to cur url
                if (!curUrl.Equals(info.url) && curUrl != null)
                {
                    //if new enter if

                    //log
                    Log(LogType.LOG, "New image, updating");

                    //store new url
                    storeData(curUrl);

                    //get image from url
                    getImage(curUrl);

                    //set wallpaper
                    DisplayPicture(curDir + "\\images\\" + DateTime.Now.ToString("MM-dd-yyyy") + ".jpg");

                    //delete old images
                    cleanUpImagesFolder();
                }
            }
        }

        //begin wallpaper code
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, String pvParam, uint fWinIni);

        private const uint SPI_SETDESKWALLPAPER = 0x14;
        private const uint SPIF_UPDATEINIFILE = 0x1;
        private const uint SPIF_SENDWININICHANGE = 0x2;

        private static void DisplayPicture(string file_name)
        {
            uint flags = 0;
            if (!SystemParametersInfo(SPI_SETDESKWALLPAPER,
                    0, file_name, flags))
            {
                Log(LogType.ERROR, "Unable to put image onto wallpaper");
            }
        }

        static void Main(string[] args)
        {
            loadData("");
            if (info.api.Equals(""))
            {
                getApiKey();
            }
            try
            {
                updateThread = new Thread(updateWallpaper);
                updateThread.Start();
            }
            catch (Exception e)
            {
                Log(LogType.ERROR, "Unable to boot app... exiting");
                System.Environment.Exit(0);
            }
            makeTrayIcon();
        }
    }
}
