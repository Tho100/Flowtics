using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NativeWifi;
using System.Net;
using Microsoft.WindowsAPICodePack.Net;
using System.Diagnostics;
using System.Management;
using System.Management.Instrumentation;
using WindowsDisplayAPI;
using System.Threading;
using OpenHardwareMonitor.Hardware;
using System.Windows.Threading;
using System.Globalization;

namespace Flowtics {
    public partial class Form1 : Form {
        public double CurrentValue { get; set; }
        public string InstanceName { get; set; }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int destIp, int srcIP, byte[] macAddr, ref uint physicalAddrLen);

        public PerformanceCounter ram_pc = new PerformanceCounter("Memory", "% Committed Bytes In Use", null);
        public PerformanceCounter ram_mb = new PerformanceCounter("Memory", "Available MBytes", null);
        public PerformanceCounter cpu_usg = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        private Thread cpuThread;
        private double[] cpuArr = new double[30];

        public int[] mbps = new int[30];
        public string IP { get; set; }
        public string MAC { get; set; }

        // DISK TIMER
        DispatcherTimer TimerDisk = new DispatcherTimer();
        // RAM TIMER
        DispatcherTimer TimerRam = new DispatcherTimer();
        // CPU TIMER
        DispatcherTimer TimerCpu = new DispatcherTimer();
        // UPTIME TIMER
        DispatcherTimer TimerUpTime = new DispatcherTimer();
        // MBPS TIMER
        DispatcherTimer TimerMbps = new DispatcherTimer();
        // SCAN THREAD
        Thread scannerThread = null;

        public event SensorEventHandler SensorAdded;
        public event SensorEventHandler SensorRemoved;

        [DllImport("wininet.dll")]

        private extern static bool InternetGetConnectedState(out int conn, int val);
        public Form1() {

            Thread setup = new Thread(new ThreadStart(StartMain));
            setup.Start();
            Thread.Sleep(1500);

            InitializeComponent();

            setup.Abort();

            Control.CheckForIllegalCrossThreadCalls = false;
            tabPage3.Enter += new System.EventHandler(tabPage3_Enter);
            tabPage2.Enter += new System.EventHandler(tabPage2_Enter);
            tabPage1.Enter += new System.EventHandler(tabPage1_Enter);

            TimerDisk.Tick += TimerDisk_Tick;
            TimerDisk.Interval = new TimeSpan(0,0,0,0, 459);
            TimerDisk.IsEnabled = true;

            TimerRam.Tick += TimerRam_Tick;
            TimerRam.Interval = new TimeSpan(0,0,0,0, 459);
            TimerRam.IsEnabled = true;

            TimerCpu.Tick += TimerCpu_Tick;
            TimerCpu.Interval = new TimeSpan(0, 0, 0, 0, 459);
            TimerCpu.IsEnabled = true;

            TimerMbps.Tick += TimerMbps_Tick;
            TimerMbps.Interval = new TimeSpan(0,0,0,0,459);
            TimerMbps.IsEnabled = true;
        }

        public void StartMain() {
            Application.Run(new SPLASH_STARTUP());
        }

        private void DisplayConnections() {
            bool connected = NetworkInterface.GetIsNetworkAvailable();
            //if (connected == true) {
            label7.Visible = true;
            IEnumerable<string> NetworkConnectionNames =
                NetworkInterface.GetAllNetworkInterfaces().Select(ni => ni.Name);
            foreach (object ssid in NetworkConnectionNames) {
                listBox1.Items.Add(ssid.ToString());
            }
            //}
        }
        string ssid_ToString(Wlan.Dot11Ssid ssid) {
            return Encoding.ASCII.GetString(ssid.SSID, 0, (int)ssid.SSIDLength);
        }
        private void Display_ANetworks() {

            WlanClient client = new WlanClient();
            foreach(WlanClient.WlanInterface wlanIface in client.Interfaces) {
                Wlan.WlanAvailableNetwork[] networks = wlanIface.GetAvailableNetworkList(0);
                foreach(Wlan.WlanAvailableNetwork network in networks) {
                    listBox3.Items.Add(ssid_ToString(network.dot11Ssid));
                    if(listBox3.Items.Contains("")) {
                        listBox3.Items.Remove("");
                    }
                }
            }
        }
        public void GetHostName() {
            try {
                string hostName = Dns.GetHostName();
                label14.Text = hostName;
            } catch (Exception eq) {
                return;
            }
        }
        public void GetIPAddr() {
            try {
                string IpAddr = Dns.GetHostByName(Dns.GetHostName()).AddressList[0].ToString();
                label20.Text = IpAddr;
            } catch (Exception eq) {
                return;
            } 
        }
  
        public string GetNetworkWeb(string ipAddr) {
            try {
                IPHostEntry entry = Dns.GetHostEntry(ipAddr);
                if(entry != null) {
                    
                }
            } catch (Exception eq) {
                //
            }
            return null;

        }
        public void GetDefaultGateway() {
            try {
                IPAddress result = null;
                var cards = NetworkInterface.GetAllNetworkInterfaces().ToList();
                if (cards.Any()) {
                    foreach (var card in cards) {
                        var props = card.GetIPProperties();
                        if (props == null) {
                            continue;
                        }

                        var gateways = props.GatewayAddresses;
                        if (!gateways.Any()) {
                            continue;
                        }

                        var gateway =
                            gateways.FirstOrDefault(g => g.Address.AddressFamily.ToString() == "InterNetwork");
                        if (gateway == null) {
                            continue;
                        }
                        result = gateway.Address;
                        break;
                    };
                }
                label23.Text = result.ToString();
                guna2TextBox1.PlaceholderText = "Default: " + result.ToString().Substring(0,9);
                guna2TextBox2.PlaceholderText = "Default: " + result.ToString().Substring(0,9) + ".255";
            } catch (Exception eq) {
                return;
            }
        }

        List<int> mbps_values = new List<int>();
        public void TimerMbps_Tick(object sender,EventArgs e) {
            WebClient webClient = new WebClient();
            DateTime dt = DateTime.Now;
            byte[] data = webClient.DownloadData("https://www.google.com/");
            DateTime dt1 = DateTime.Now;
            var setup = (data.Length * 8) / (dt1 - dt).TotalSeconds;
            label26.Text = (setup/1024).ToString().Substring(0,2);

            mbps_values.Add(Convert.ToInt32(label26.Text));
            label105.Text = mbps_values.Min().ToString();
            label127.Text = mbps_values.Max().ToString();

            if(Convert.ToInt32(label26.Text) >= 20) {
               // label26.ForeColor = ColorTranslator.FromHtml("#07e007");
            } else {
               // label26.ForeColor = Color.Red;
            }

            if(Convert.ToInt32(label105.Text) >= 20) {
                label105.ForeColor = ColorTranslator.FromHtml("#07e007");
            }
            else {
                label105.ForeColor = Color.Red;
            }

            if (Convert.ToInt32(label127.Text) >= 20) {
                label127.ForeColor = ColorTranslator.FromHtml("#07e007");
            }
            else {
                label127.ForeColor = Color.Red;
            }

            chart2.Series["Mbps"].Points.Add(Convert.ToInt32(label26.Text));
            chart2.Series["Mbps"].Color = Color.FromArgb(128, ColorTranslator.FromHtml("#1F75FE"));

            chart2.Series["Bytes Received"].Points.Add(Convert.ToInt32(label26.Text));
            chart2.Series["Bytes Received"].Color = ColorTranslator.FromHtml("#03254c");
            chart2.Series["Bytes Received"].IsVisibleInLegend = false;
        }
        public void determineType() {
            try {
                // Determine if connection is Enthernet or Wi-fi
                foreach(NetworkInterface netFace in NetworkInterface.GetAllNetworkInterfaces()) {
                    if(netFace.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                        || netFace.NetworkInterfaceType == NetworkInterfaceType.Ethernet) {
                        label4.Text = netFace.NetworkInterfaceType.ToString();  
                    }
                }
                // Determine its status (Private,Public)
                //var manager = new NetworkListManagerClass();
                //var connectedNetworks = manager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED).Cast<INetwork>();
                //foreach (var network in connectedNetworks) {
                //    Console.Write(network.GetName() + " ");
                //    var cat = network.GetCategory();
                //    if (cat == NLM_NETWORK_CATEGORY.NLM_NETWORK_CATEGORY_PRIVATE)
                //        Console.WriteLine("[PRIVATE]");
                //    else if (cat == NLM_NETWORK_CATEGORY.NLM_NETWORK_CATEGORY_PUBLIC)
                //       Console.WriteLine("[PUBLIC]");
                //    else if (cat == NLM_NETWORK_CATEGORY.NLM_NETWORK_CATEGORY_DOMAIN_AUTHENTICATED)
                //        Console.WriteLine("[DOMAIN]");
                //}
                //Console.ReadKey();

            } catch (Exception) {
                label4.Text = "Unknown";
            }
        }

        public void NetSentRecv(NetworkInterfaceComponent version) {
            try {
                for(int i=0; i<10; i++) {
                    IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                    UdpStatistics udpStat = null;

                    switch (version) {
                        case NetworkInterfaceComponent.IPv4:
                            udpStat = properties.GetUdpIPv4Statistics();
                            break;
                        case NetworkInterfaceComponent.IPv6:
                            udpStat = properties.GetUdpIPv6Statistics();
                            break;
                        default:
                            throw new ArgumentException("version");
                            //    break;
                    }
                    label109.Text = (udpStat.DatagramsReceived).ToString();
                    label113.Text = (udpStat.DatagramsSent).ToString();
                    //Console.WriteLine("  Incoming Datagrams Discarded ............ : {0}",
                    //    udpStat.IncomingDatagramsDiscarded);
                    //Console.WriteLine("  Incoming Datagrams With Errors .......... : {0}",
                    //    udpStat.IncomingDatagramsWithErrors);
                    //Console.WriteLine("  UDP Listeners ........................... : {0}",
                    //    udpStat.UdpListeners);
                    //Console.WriteLine("");
                }
            } catch (Exception) {
                //
            }
        }



        bool isConnected() {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        public static string SendBackProcessorName() {
            ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            string Procname = null;

            foreach (ManagementObject moProcessor in mosProcessor.Get()) {
                if (moProcessor["name"] != null) {
                    Procname = moProcessor["name"].ToString();
                }
            }
            return Procname;
        }
        public void startScanning() {
            var ip_DefaultScan = label23.Text.Substring(0, 9);
            scannerThread = new Thread(() => scan(ip_DefaultScan));
            scannerThread.IsBackground = true;
            scannerThread.Start();
        }
        int second = 0;
        private void Form1_Load(object sender, EventArgs e) {
            this.WindowState = FormWindowState.Maximized;
            this.Text = "Flowtics: Network & Machine Monitoring";

            ToolTip sett = new ToolTip();
            sett.SetToolTip(this.guna2Button10,"Stop Scan");

            string macAddr = NetworkInterface.GetAllNetworkInterfaces()
                            .Where(f => f.OperationalStatus == OperationalStatus.Up && f.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                            .Select(f => f.GetPhysicalAddress().ToString())
                            .FirstOrDefault();
            try {
                int current_tab = guna2TabControl1.SelectedIndex;
                string tab_name = guna2TabControl1.TabPages[current_tab].Text;
                if(tab_name == "Network") {
                    if(isConnected() == true) {
                        // SSID name and con type
                        var networks = NetworkListManager.GetNetworks(NetworkConnectivityLevels.Connected);
                        foreach (var network in networks) {
                            label16.Text = network.Name;
                            label18.Text = network.Category.ToString();
                        }

                        // ARP LOOKup
                        string hostName = Dns.GetHostName();
                  
                        second = 2;
                        timer1.Start();

                        DisplayConnections();
                        Display_ANetworks();
                        determineType();
                        GetHostName();
                        GetIPAddr();
                        GetDefaultGateway();     
                        startScanning();
                    }
                    else {
                        label10.Visible = true;
                    }
                }             
            } catch (Exception) {
                startScanning();
                MessageBox.Show("Wi-fi Network not detected, some features might not available.","Flowtics System",
                    MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }

        private void guna2Panel1_Paint(object sender, PaintEventArgs e) {

        }

        private void tabPage1_Enter(object sender, EventArgs e) {
            chart2.ChartAreas[0].AxisY.Title = "Mbps";
            chart2.ChartAreas[0].AxisY.TitleFont = new Font("Segoe UI", 14, FontStyle.Regular);
            chart2.ChartAreas[0].AxisY.TitleForeColor = Color.White;

            chart2.ChartAreas[0].AxisY.LabelStyle.ForeColor = ColorTranslator.FromHtml("#F5F5F5");
            chart2.ChartAreas[0].AxisX.LabelStyle.ForeColor = ColorTranslator.FromHtml("#F5F5F5");

            chart2.ChartAreas[0].AxisY.LineColor = Color.Gray;//ColorTranslator.FromHtml("#232323");
            chart2.ChartAreas[0].AxisX.LineColor = Color.Gray; //ColorTranslator.FromHtml("#232323");

            chart2.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.Gray;//ColorTranslator.FromHtml("#D0D0D0");
            chart2.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.Gray;//ColorTranslator.FromHtml("#D0D0D0");

            chart2.BackColor = ColorTranslator.FromHtml("#232323");
            chart2.ChartAreas[0].BackColor = Color.Transparent;

            //chart2.Series["Bytes Sent"].Color = ColorTranslator.FromHtml("#3C99DC");
            //chart2.Series["Bytes Received"].Color = ColorTranslator.FromHtml("#0F5298");

            //chart1.Series[1].Color = ColorTranslator.FromHtml("#00CEF1");
            //chart2.Series[0].Color = Color.FromArgb(128, ColorTranslator.FromHtml("#1F75FE"));
        }
        public string ProcessorName() {
            ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            string Procname = null;

            foreach (ManagementObject moProcessor in mosProcessor.Get()) {
                if (moProcessor["name"] != null) {
                    Procname = moProcessor["name"].ToString();
                    label35.Text = Procname;
                }

            }

            return Procname;
        }

        public void totalCores() {
            try {
                int core_count = 0;
                foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get()) {
                    core_count += int.Parse(item["NumberOfCores"].ToString());
                }
                label39.Text = core_count.ToString();
            } catch (Exception) {
                return;
            }
        }
        public void totalLogicalProc() {
            label40.Text = Environment.ProcessorCount.ToString();
        }
        public void totalPhysicalProc() {
            try {
                foreach(var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get()) {
                    var t_pr = item["NumberOfProcessors"];
                    label41.Text = t_pr.ToString();
                }
            } catch (Exception) {
                return;
           }
        }
        public void check_VT() {
            foreach(var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get()) {
                var check = item["VirtualizationFirmwareEnabled"];
                label43.Text = check.ToString();
            }
        }
        public PerformanceCounter disk_pc = new PerformanceCounter("PhysicalDisk","% Disk Time","_Total");
        public Int32 jq_disk = 0;
        public void TimerDisk_Tick(object sender, EventArgs e) {
            jq_disk = Convert.ToInt32(disk_pc.NextValue());
            label118.Text = jq_disk.ToString() + "%";
        }

        public void DiskSection() {
            try {
                int billion = 1000000000;
                DriveInfo[] myDrives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in myDrives) {
                    if (drive.IsReady == true) {
                        label54.Text = drive.DriveType.ToString();
                        label45.Text = drive.DriveFormat;
                        var byte_size = (drive.AvailableFreeSpace)/billion;
                        label47.Text = byte_size.ToString() + "GB";
                    }
                }

                var finder = new ManagementObjectSearcher(@"Select * from Win32_DiskDrive");
                foreach(ManagementObject obj in finder.Get()) {
                    var capacity = Convert.ToInt64(obj["Size"])/billion;
                    label49.Text = capacity.ToString() + "GB";
                    label52.Text = obj["Name"].ToString();
                }

                ManagementScope scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM MSFT_PhysicalDisk");
                string type = "";
                scope.Connect();
                searcher.Scope = scope;

                foreach (ManagementObject queryObj in searcher.Get()) {
                    switch (Convert.ToInt16(queryObj["MediaType"])) {
                        case 1:
                            type = "Unspecified";
                            break;

                        case 3:
                            type = "HDD";
                            break;

                        case 4:
                            type = "SSD";
                            break;

                        case 5:
                            type = "SCM";
                            break;

                        default:
                            type = "Unspecified";
                            break;
                    }
                }
                searcher.Dispose();
                label46.Text = type;

                WqlObjectQuery q = new WqlObjectQuery("SELECT * FROM Win32_DiskDrive");
                ManagementObjectSearcher res = new ManagementObjectSearcher(q);
                foreach (ManagementObject o in res.Get()) {
                    // Model
                    label58.Text = o["Model"].ToString();
                    // Serial No.
                    label64.Text = o["SerialNumber"].ToString();
                }
            }
            catch (Exception eq) {
                MessageBox.Show("Problem with retrieving disk information","Flowtics System",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        }

        private void MonitorSection() {
            try {
                // RESOLUTION
                string sc_Width = Screen.PrimaryScreen.Bounds.Width.ToString();
                string sc_Height = Screen.PrimaryScreen.Bounds.Height.ToString();
                label71.Text = sc_Width;
                label56.Text = sc_Height;
                    
                // NAME
                foreach(var display in Display.GetDisplays()) {
                    label74.Text = display.DeviceName;
                }

                // MANUFACTURER
                using(ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor")) {
                    foreach(ManagementObject currentObj in searcher.Get()) {
                        string name = currentObj["Name"].ToString();
                        string dev_id = currentObj["DeviceID"].ToString();
                        label84.Text = dev_id;
                    }
                }

            } catch (Exception eq) {
                MessageBox.Show("Problem with retrieving monitor information", "Flowtics System",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void tabPage3_Enter(object sender, EventArgs e) {
            // CPU
            try {
                ProcessorName();
                totalCores();
                totalLogicalProc();
                totalPhysicalProc();
                check_VT();
                getCPUClock();
            } catch (Exception er) {
                MessageBox.Show("Problem with retrieving CPU information", "Flowtics System",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // DISK
            DiskSection();
            // MONITOR
            MonitorSection();
        }

        private void tabPage1_Click(object sender, EventArgs e) {
        }

        private void label1_Click(object sender, EventArgs e) {

        }

        private void label2_Click(object sender, EventArgs e) {

        }

        private void label3_Click(object sender, EventArgs e) {

        }

        private void label5_Click(object sender, EventArgs e) {

        }

        private void label9_Click(object sender, EventArgs e) {

        }

        private void label7_Click(object sender, EventArgs e) {

        }

        private void label10_Click(object sender, EventArgs e) {

        }

        private void label8_Click(object sender, EventArgs e) {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void label4_Click(object sender, EventArgs e) {

        }

        private void guna2Panel3_Paint(object sender, PaintEventArgs e) {

        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void guna2Button1_Click(object sender, EventArgs e) {
            listBox3.Visible = false;
            guna2Button1.Visible = false;
            guna2Button2.Visible = true;
        }

        private void guna2Button2_Click(object sender, EventArgs e) {
            listBox3.Visible = true;
            guna2Button1.Visible = true;
            guna2Button2.Visible = false;
        }

        private void label16_Click(object sender, EventArgs e) {

        }

        private void label17_Click(object sender, EventArgs e) {

        }

        private void guna2Panel5_Paint(object sender, PaintEventArgs e) {

        }

        private void listBox3_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void label14_Click(object sender, EventArgs e) {

        }

        private void guna2Panel8_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel7_Paint(object sender, PaintEventArgs e) {

        }

        private void label3_Click_1(object sender, EventArgs e) {

        }

        private void listBox4_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void listBox5_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void label5_Click_1(object sender, EventArgs e) {

        }

        private void label12_Click(object sender, EventArgs e) {

        }

        private void label15_Click(object sender, EventArgs e) {

        }

        private void guna2Panel4_Paint(object sender, PaintEventArgs e) {

        }

        private void label13_Click(object sender, EventArgs e) {

        }

        private void label20_Click(object sender, EventArgs e) {

        }

        private void guna2Panel6_Paint(object sender, PaintEventArgs e) {

        }

        private void label24_Click(object sender, EventArgs e) {

        }

        private void guna2Panel10_Paint(object sender, PaintEventArgs e) {

        }

        public void refreshment() {
            try {
                second--;
                if (second == 1) {
                    timer1.Stop();

                    //SignalStrength();
                    isConnected();
                    PerfomanceMeter();
                    NetSentRecv(NetworkInterfaceComponent.IPv4);

                    if (Convert.ToInt32(label26.Text) >= 10) {
                        label8.Text = "Good";
                        label8.ForeColor = ColorTranslator.FromHtml("#07e007");
                    } else {
                        label8.Text = "Bad";
                        label8.ForeColor = Color.Red;
                    }

                    second = 2;
                    timer1.Start();
                }
            } catch (Exception) {
                throw;
            }
        } 

        public void PerfomanceMeter() {
            //label27.Text = (int)perfMemCounter.NextValue() + "MB";
            //chart2.Series["Mbps"].Points.Add(Convert.ToInt32(label25.Text));
            //chart2.Series["Bytes Received"].Points.Add(Convert.ToInt32(label109.Text));
            //chart2.Series["Bytes Sent"].Points.Add(Convert.ToInt32(label113.Text));
            //label105.Text = mbps.Max().ToString();
            //label109.Text = mbps.Min().ToString();
        }
        // RAM USAGE
        public Int32 ram_usg = 0;
        public Int32 ram_avai = 0;
        public void TimerRam_Tick(object sender, EventArgs e) {
            ram_usg = Convert.ToInt32(ram_pc.NextValue());
            label33.Text = ram_usg.ToString() + "%";
            label104.Text = (ram_usg/2).ToString() + "%";

            ram_avai = Convert.ToInt32(ram_mb.NextValue());
            label27.Text = ram_avai.ToString() + "MB";

            chart1.Series["RAM"].Points.Add(ram_usg);
        }

        private void retrieve_memUsage() {
            var wmiObject = new ManagementObjectSearcher("select * from Win32_OperatingSystem");

            var memoryValues = wmiObject.Get().Cast<ManagementObject>().Select(mo => new {
                FreePhysicalMemory = Double.Parse(mo["FreePhysicalMemory"].ToString()),
                TotalVisibleMemorySize = Double.Parse(mo["TotalVisibleMemorySize"].ToString())
            }).FirstOrDefault();

            if (memoryValues != null) {
                var percent = ((memoryValues.TotalVisibleMemorySize - memoryValues.FreePhysicalMemory) / memoryValues.TotalVisibleMemorySize) * 100;
                var ram_usage = percent;
                chart1.Series["RAM"].Points.Add(ram_usage);
            }
        }
        // CPU USAGE
        public Int32 cp_t = 0;
        public void TimerCpu_Tick(object sender, EventArgs e) {
            cp_t = Convert.ToInt32(cpu_usg.NextValue());
            label28.Text = cp_t.ToString() + "%";

                       label91.Text = cpuArr.Max().ToString() + "%";
            label89.Text = cpuArr.Min().ToString() + "%";
            label93.Text = cpuArr.Average().ToString().Substring(0,1) + "%";

            chart1.Series["CPU"].Points.Add(cp_t);
        }
        private void timer1_Tick(object sender, EventArgs e) {
            try {
                refreshment();
            } catch (Exception eq) {
                refreshment();
            }
        }

        private void label11_Click(object sender, EventArgs e) {

        }

        private void guna2Panel2_Paint(object sender, PaintEventArgs e) {

        }

        private void label18_Click(object sender, EventArgs e) {

        }

        private void label23_Click(object sender, EventArgs e) {

        }

        private void label21_Click(object sender, EventArgs e) {

        }

        private void label22_Click(object sender, EventArgs e) {

        }

        private void tabPage2_Click(object sender, EventArgs e) {

        }
        private void getPerfCounter() {
            try {
                var cpuPerfCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total");
                while (true) {
                    // CPU
                    cpuArr[cpuArr.Length - 1] = Math.Round(cpuPerfCounter.NextValue(),0);
                    Array.Copy(cpuArr,1,cpuArr,0,cpuArr.Length-1);
                    if(chart1.IsHandleCreated) {
                        this.Invoke((MethodInvoker) delegate {UpdateChart();});
                    }
                    Thread.Sleep(199);
                }
            } catch (Exception) {
                throw;
            }
        }

        private void UpdateChart() {
            //try {
            //chart1.Series["CPU"].Points.Clear();
            for(int i=0; i<cpuArr.Length-1; i++) {
                //chart1.Series["CPU"].Points.Add(cpuArr[i]);
            }
        }

        private void GetBoardTemp() {
            double temp = 0;
            string inst_name = "";

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\WMI","SELECT * FROM MSAcpi_ThermalZoneTemperature");
            foreach(ManagementObject obj in searcher.Get()) {
                temp = Convert.ToDouble(obj["CurrentTemperature"].ToString());
                temp = (temp - 2732)/10.0;
                inst_name = obj["InstanceName"].ToString();
                //label99.Text = temp.ToString();
            } 
        }

        float cpuTemp;
        static Computer c = new Computer() {
            CPUEnabled = true
        };
        public void getCPUTemp() {
            c.Open();
            foreach(var hardware in c.Hardware) {
                if(hardware.HardwareType == HardwareType.CPU) {
//                    hardware.Update();
                    // TEMP
                    foreach(var sensor in hardware.Sensors) {
                        if(sensor.SensorType == SensorType.Power && sensor.Name.Contains("CPU Package")) {
                            cpuTemp = sensor.Value.GetValueOrDefault();
                        }
                    }
                }
            }
            //label95.Text = cpuTemp.ToString();
        }

        public void GetCPUVolt() {
            using (var managementObjSearcher = new ManagementObjectSearcher("Select * from Win32_Processor")) {
                foreach (var item in managementObjSearcher.Get()) {
                    int v = (ushort)(item["CurrentVoltage"]) / 10;
                    label95.Text = v.ToString();
                }
            }
        }

        public void getCPUClock() {
            using (var managementObjSearcher = new ManagementObjectSearcher("Select * from Win32_Processor")) {
                foreach(var item in managementObjSearcher.Get()) {
                    label86.Text = (decimal.Parse(item["CurrentClockSpeed"].ToString())/1000).ToString() + "Ghz";
                }
            }
        }

        public void TimerUpTime_Tick(object sender, EventArgs e) {
            try {
                var ticks = Stopwatch.GetTimestamp();
                var upTime = ((double)ticks)/Stopwatch.Frequency;
                var upTimeSpan = TimeSpan.FromSeconds(upTime);
                label126.Text = upTimeSpan.ToString().Substring(0,10);
            } catch (Exception) {
                //
            }
        }

        private void tabPage2_Enter(object sender, EventArgs e) {

            TimerUpTime.Tick += TimerUpTime_Tick;
            TimerUpTime.Interval = new TimeSpan(0, 0, 0, 0, 459);
            TimerUpTime.IsEnabled = true;

            cpuThread = new Thread(new ThreadStart(this.getPerfCounter));
            cpuThread.IsBackground = true;
            cpuThread.Start();

            //GetBoardTemp();
            //getCPUTemp();

            chart1.ChartAreas[0].AxisY.Title = "Usage";
            chart1.ChartAreas[0].AxisY.TitleFont = new Font("Segoe UI",14,FontStyle.Regular);
            chart1.ChartAreas[0].AxisY.TitleForeColor = Color.White;

            chart1.ChartAreas[0].AxisY.LabelStyle.ForeColor = ColorTranslator.FromHtml("#F5F5F5");
            chart1.ChartAreas[0].AxisX.LabelStyle.ForeColor = ColorTranslator.FromHtml("#F5F5F5");

            //  chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
            //  chart1.ChartAreas[0].AxisX.MinorGrid.Enabled = false;
            //  chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
            //  chart1.ChartAreas[0].AxisY.MinorGrid.Enabled = false;

            chart1.ChartAreas[0].AxisY.LineColor = Color.Gray;//ColorTranslator.FromHtml("#232323");
            chart1.ChartAreas[0].AxisX.LineColor = Color.Gray; //ColorTranslator.FromHtml("#232323");

            chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.Gray;//ColorTranslator.FromHtml("#D0D0D0");
            chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.Gray;//ColorTranslator.FromHtml("#D0D0D0");

            chart1.BackColor = ColorTranslator.FromHtml("#232323");
            chart1.ChartAreas[0].BackColor = Color.Transparent;
            
            //chart1.Series[1].Color = ColorTranslator.FromHtml("#00CEF1");
            chart1.Series[1].Color = Color.FromArgb(128,ColorTranslator.FromHtml("#1F75FE"));
            
    }
       

        private void label26_Click(object sender, EventArgs e) {

        }

        private void label28_Click(object sender, EventArgs e) {

        }

        private void label31_Click(object sender, EventArgs e) {

        }

        private void label31_Click_1(object sender, EventArgs e) {

        }

        private void guna2ProgressBar1_ValueChanged(object sender, EventArgs e) {

        }

        private void label32_Click(object sender, EventArgs e) {

        }

        private void label33_Click(object sender, EventArgs e) {

        }

        private void guna2Panel12_Paint(object sender, PaintEventArgs e) {
      
        }

        private void chart1_Click(object sender, EventArgs e) {

        }

        private void label27_Click(object sender, EventArgs e) {

        }
        private void label33_Click_1(object sender, EventArgs e) {

        }

        private void tabPage3_Click(object sender, EventArgs e) {

        }

        private void chart2_Click(object sender, EventArgs e) {

        }

        private void label34_Click(object sender, EventArgs e) {

        }

        private void label35_Click(object sender, EventArgs e) {

        }

        private void guna2Button3_Click(object sender, EventArgs e) {
            foreach(var series in chart1.Series) {
                series.Points.Clear();
            }
        }

        private void chart2_Click_1(object sender, EventArgs e) {

        }

        private void guna2Panel15_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Button4_Click(object sender, EventArgs e) {
            try {
                foreach(var series in chart2.Series) {
                    series.Points.Clear();
                }
            } catch (Exception) {
                //
            }
        }

        private void guna2Panel16_Paint(object sender, PaintEventArgs e) {

        }

        private void label31_Click_2(object sender, EventArgs e) {

        }

        private void label26_Click_1(object sender, EventArgs e) {

        }

        private void label25_Click(object sender, EventArgs e) {

        }

        private void label34_Click_1(object sender, EventArgs e) {

        }

        private void guna2PictureBox1_Click(object sender, EventArgs e) {

        }

        private void label35_Click_1(object sender, EventArgs e) {

        }

        private void label37_Click(object sender, EventArgs e) {

        }

        private void guna2Panel18_Paint(object sender, PaintEventArgs e) {

        }

        private void label39_Click(object sender, EventArgs e) {

        }

        private void label41_Click(object sender, EventArgs e) {

        }

        private void label42_Click(object sender, EventArgs e) {

        }

        private void label43_Click(object sender, EventArgs e) {

        }

        private void label53_Click(object sender, EventArgs e) {

        }

        private void guna2PictureBox2_Click(object sender, EventArgs e) {

        }

        private void label48_Click(object sender, EventArgs e) {

        }

        private void label44_Click(object sender, EventArgs e) {

        }

        private void label45_Click(object sender, EventArgs e) {

        }

        private void label51_Click(object sender, EventArgs e) {

        }

        private void label45_Click_1(object sender, EventArgs e) {

        }

        private void guna2Panel19_Paint(object sender, PaintEventArgs e) {

        }

        private void label47_Click(object sender, EventArgs e) {

        }

        private void label48_Click_1(object sender, EventArgs e) {

        }

        private void label46_Click(object sender, EventArgs e) {

        }

        private void label49_Click(object sender, EventArgs e) {

        }

        private void guna2Button5_Click(object sender, EventArgs e) {
            label20.Visible = false;
            guna2Button5.Visible = false;
            guna2Button6.Visible = true;
        }

        private void guna2Button7_Click(object sender, EventArgs e) {
            label23.Visible = false;
            guna2Button7.Visible = false;
            guna2Button8.Visible = true;
        }

        private void guna2Button6_Click(object sender, EventArgs e) {
            guna2Button5.Visible = true;
            guna2Button6.Visible = false;
            label20.Visible = true;
        }

        private void guna2Button8_Click(object sender, EventArgs e) {
            label23.Visible = true;
            guna2Button7.Visible = true;
            guna2Button8.Visible = false;
        }
        private void scan(string subnet) {
            Ping myPing;
            PingReply p_r;
            IPAddress addr;
            IPHostEntry host;
            try {
                for(int i=1; i<255; i++) {
                    string subnet_t = "." + i.ToString();
                    myPing = new Ping();
                    p_r = myPing.Send(subnet+subnet_t);

                    if(p_r.Status == IPStatus.Success) {
                        addr = IPAddress.Parse(subnet+subnet_t);
                        host = Dns.GetHostEntry(addr);

                        if (listBox2.Items.Contains(subnet + subnet_t)) {
                            //
                        }
                        else {
                            
                        }

                        if (listBox4.Items.Contains(host.HostName.ToString())) {
                            //
                        }
                        else {
                            listBox2.Items.Add(subnet + subnet_t);
                            listBox4.Items.Add(host.HostName.ToString());
                            listBox5.Items.Add("Active");
                        }

                        int total_devices = listBox2.Items.Count;
                        label106.Text = total_devices.ToString();
                    }
                }
            } catch (ThreadStartException eq) {
                MessageBox.Show("Unknown error while attempting to scan..", "Flowtics System",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            label116.Visible = false;
        }
       

        private void label106_Click(object sender, EventArgs e) {

        }

        private void tabPage5_Click(object sender, EventArgs e) {

        }

        private void tabPage4_Click(object sender, EventArgs e) {

        }

        private void guna2Panel23_Paint(object sender, PaintEventArgs e) {

        }

        private void label78_Click(object sender, EventArgs e) {

        }

        private void label79_Click(object sender, EventArgs e) {

        }

        private void label80_Click(object sender, EventArgs e) {

        }

        private void label81_Click(object sender, EventArgs e) {

        }

        private void guna2PictureBox6_Click(object sender, EventArgs e) {

        }

        private void label82_Click(object sender, EventArgs e) {

        }

        private void label83_Click(object sender, EventArgs e) {

        }

        private void guna2Panel22_Paint(object sender, PaintEventArgs e) {

        }

        private void label68_Click(object sender, EventArgs e) {

        }

        private void label69_Click(object sender, EventArgs e) {

        }

        private void label70_Click(object sender, EventArgs e) {

        }

        private void label72_Click(object sender, EventArgs e) {

        }

        private void guna2PictureBox5_Click(object sender, EventArgs e) {

        }

        private void label76_Click(object sender, EventArgs e) {

        }

        private void label77_Click(object sender, EventArgs e) {

        }

        private void guna2Panel21_Paint(object sender, PaintEventArgs e) {

        }

        private void label60_Click(object sender, EventArgs e) {

        }

        private void label61_Click(object sender, EventArgs e) {

        }

        private void label62_Click(object sender, EventArgs e) {

        }

        private void label63_Click(object sender, EventArgs e) {

        }

        private void guna2PictureBox4_Click(object sender, EventArgs e) {

        }

        private void label66_Click(object sender, EventArgs e) {

        }

        private void label67_Click(object sender, EventArgs e) {

        }

        private void guna2Panel20_Paint(object sender, PaintEventArgs e) {

        }

        private void label84_Click(object sender, EventArgs e) {

        }

        private void label85_Click(object sender, EventArgs e) {

        }

        private void label56_Click(object sender, EventArgs e) {

        }

        private void label57_Click(object sender, EventArgs e) {

        }

        private void label71_Click(object sender, EventArgs e) {

        }

        private void label73_Click(object sender, EventArgs e) {

        }

        private void guna2PictureBox3_Click(object sender, EventArgs e) {

        }

        private void label74_Click(object sender, EventArgs e) {

        }

        private void label75_Click(object sender, EventArgs e) {

        }

        private void label64_Click(object sender, EventArgs e) {

        }

        private void label65_Click(object sender, EventArgs e) {

        }

        private void label58_Click(object sender, EventArgs e) {

        }

        private void label59_Click(object sender, EventArgs e) {

        }

        private void label49_Click_1(object sender, EventArgs e) {

        }

        private void label50_Click(object sender, EventArgs e) {

        }

        private void label54_Click(object sender, EventArgs e) {

        }

        private void label55_Click(object sender, EventArgs e) {

        }

        private void label52_Click(object sender, EventArgs e) {

        }

        private void label86_Click(object sender, EventArgs e) {

        }

        private void label87_Click(object sender, EventArgs e) {

        }

        private void label40_Click(object sender, EventArgs e) {

        }

        private void label38_Click(object sender, EventArgs e) {

        }

        private void label36_Click(object sender, EventArgs e) {

        }

        private void guna2Panel34_Paint(object sender, PaintEventArgs e) {

        }

        private void label103_Click(object sender, EventArgs e) {

        }

        private void label104_Click(object sender, EventArgs e) {

        }

        private void guna2Panel11_Paint(object sender, PaintEventArgs e) {

        }

        private void label30_Click(object sender, EventArgs e) {

        }

        private void guna2Panel33_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel28_Paint(object sender, PaintEventArgs e) {

        }

        private void label102_Click(object sender, EventArgs e) {

        }

        private void guna2Panel31_Paint(object sender, PaintEventArgs e) {

        }

        private void label98_Click(object sender, EventArgs e) {

        }

        private void label99_Click(object sender, EventArgs e) {

        }

        private void guna2Panel29_Paint(object sender, PaintEventArgs e) {

        }

        private void label94_Click(object sender, EventArgs e) {

        }

        private void label95_Click(object sender, EventArgs e) {

        }

        private void guna2Panel32_Paint(object sender, PaintEventArgs e) {

        }

        private void label100_Click(object sender, EventArgs e) {

        }

        private void label101_Click(object sender, EventArgs e) {

        }

        private void guna2Panel30_Paint(object sender, PaintEventArgs e) {

        }

        private void label96_Click(object sender, EventArgs e) {

        }

        private void label97_Click(object sender, EventArgs e) {

        }

        private void guna2Panel27_Paint(object sender, PaintEventArgs e) {

        }

        private void label92_Click(object sender, EventArgs e) {

        }

        private void label93_Click(object sender, EventArgs e) {

        }

        private void guna2Panel25_Paint(object sender, PaintEventArgs e) {

        }

        private void label88_Click(object sender, EventArgs e) {

        }

        private void label89_Click(object sender, EventArgs e) {

        }

        private void guna2Panel26_Paint(object sender, PaintEventArgs e) {

        }

        private void label90_Click(object sender, EventArgs e) {

        }

        private void label91_Click(object sender, EventArgs e) {

        }

        private void guna2Panel14_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel10_Paint_1(object sender, PaintEventArgs e) {

        }

        private void label29_Click(object sender, EventArgs e) {

        }

        private void guna2Panel13_Paint(object sender, PaintEventArgs e) {

        }

        private void label32_Click_1(object sender, EventArgs e) {

        }

        private void guna2Panel17_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e) {

        }

        private void guna2Panel9_Paint(object sender, PaintEventArgs e) {

        }

        private void label105_Click(object sender, EventArgs e) {

        }

        private void listBox5_SelectedIndexChanged_1(object sender, EventArgs e) {

        }

        public string IPMac(string ipAddress) {
            string macAddress = string.Empty;
            System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
            pProcess.StartInfo.FileName = "arp";
            pProcess.StartInfo.Arguments = "-a " + ipAddress;
            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.RedirectStandardOutput = true;
            pProcess.StartInfo.CreateNoWindow = true;
            pProcess.Start();
            string strOutput = pProcess.StandardOutput.ReadToEnd();
            string[] substrings = strOutput.Split('-');
            if (substrings.Length >= 8) {
                macAddress = substrings[3].Substring(Math.Max(0, substrings[3].Length - 2))
                         + "-" + substrings[4] + "-" + substrings[5] + "-" + substrings[6]
                         + "-" + substrings[7] + "-"
                         + substrings[8].Substring(0, 2);
                return macAddress;
            }

            else {
                return "Unknown";
            }
        }

        private void listBox4_SelectedIndexChanged_1(object sender, EventArgs e) {
            try {
                var host = listBox4.GetItemText(listBox4.SelectedItem);
                var host_indx = listBox4.SelectedIndex;
                var host_ip = listBox2.Items[host_indx].ToString();
                var item_mac = IPMac(host_ip);
                if (item_mac != null) {
                    string device_name = host;
                    Form2 details = new Form2(device_name,item_mac,host_ip,this);
                    details.Show();
                }
            } catch (Exception eq) {
                MessageBox.Show("Could not view the details of this device\nPotential causes:\n\nMAC address could not be detect\nYou're not selecting any host"
                    ,"Flowtics System",
                    MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        private void listBox2_SelectedIndexChanged_1(object sender, EventArgs e) {

        }

        private void label19_Click(object sender, EventArgs e) {

        }

        private void label6_Click(object sender, EventArgs e) {

        }

        private void guna2TabControl1_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void label108_Click(object sender, EventArgs e) {

        }

        private void label114_Click(object sender, EventArgs e) {

        }

        private void label109_Click(object sender, EventArgs e) {

        }

        private void guna2Panel38_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel35_Paint(object sender, PaintEventArgs e) {

        }

        private void label105_Click_1(object sender, EventArgs e) {

        }

        private void label115_Click(object sender, EventArgs e) {

        }

        private void label108_Click_1(object sender, EventArgs e) {

        }

        private void label113_Click(object sender, EventArgs e) {

        }

        private void label111_Click(object sender, EventArgs e) {

        }

        private void scanCustom(string subnet_ip,int range) {
            Ping ping;
            PingReply p_r;
            IPAddress addr;
            IPHostEntry host;

            for(int i=1; i<range+1; i++) {
                string subnet_t = "." + i.ToString();
                ping = new Ping();
                p_r = ping.Send(subnet_ip+subnet_t);

                if(p_r.Status == IPStatus.Success) {
                    try {
                        label116.Visible = true;
                        addr = IPAddress.Parse(subnet_ip+subnet_t);
                        host = Dns.GetHostEntry(addr);

                        if (listBox2.Items.Contains(subnet_ip + subnet_t)) {
                            //
                        }
                        else {

                        }

                        if (listBox4.Items.Contains(host.HostName.ToString())) {
                            //
                        }
                        else {
                            listBox2.Items.Add(subnet_ip + subnet_t);
                            listBox4.Items.Add(host.HostName.ToString());
                            listBox5.Items.Add("Active");
                        }
                        int total_devices = listBox2.Items.Count;
                        label106.Text = total_devices.ToString();

                    } catch (Exception eq) {
                        //
                    }
                }
            }
            label116.Visible = false;
            guna2Button10.Visible = false;
        }
        Thread custThread;
        private void guna2Button9_Click(object sender, EventArgs e) {
            guna2Button10.Visible = true;
            listBox4.Items.Clear();
            listBox5.Items.Clear();
            listBox2.Items.Clear();
            var ip_Set = guna2TextBox1.Text;
            var range_Set = guna2TextBox2.Text;
            try {   
                if(range_Set != "" && ip_Set != "") {
                    var last_Range = Convert.ToInt32(range_Set.Substring(range_Set.LastIndexOf('.')+1));
                    custThread = new Thread(() => scanCustom(ip_Set,last_Range));
                    custThread.IsBackground = true;
                    custThread.Start();
                } else {
                    custThread = new Thread(() => scanCustom(label23.Text.Substring(0, 9), 255));
                    custThread.IsBackground = true;
                    custThread.Start();
                }
            } catch (Exception eq) {
            MessageBox.Show("Unknown error while attempting to scan.\nAre you connected to the internet?"
                , "Flowtics System", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label116_Click(object sender, EventArgs e) {

        }

        private void label112_Click(object sender, EventArgs e) {
            
        }

        private void label117_Click(object sender, EventArgs e) {

        }

        private void guna2Panel40_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel42_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel43_Paint(object sender, PaintEventArgs e) {

        }

        private void label123_Click(object sender, EventArgs e) {

        }

        private void label118_Click(object sender, EventArgs e) {

        }

        private void guna2TextBox2_TextChanged(object sender, EventArgs e) {

        }

        private void guna2TextBox1_TextChanged_1(object sender, EventArgs e) {

        }

        private void tabPage6_Click(object sender, EventArgs e) {

        }

        private void guna2Panel37_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel36_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel24_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Panel17_Paint_1(object sender, PaintEventArgs e) {

        }

        private void label125_Click(object sender, EventArgs e) {

        }

        private void label107_Click(object sender, EventArgs e) {

        }

        private void label128_Click(object sender, EventArgs e) {

        }

        private void guna2Panel44_Paint(object sender, PaintEventArgs e) {

        }

        private void guna2Button10_Click(object sender, EventArgs e) {
            label116.Visible = false;
            guna2Button10.Visible = false;
            scannerThread.Abort();
        }

        private void guna2Button11_Click(object sender, EventArgs e) {

        }

        private void guna2Button15_Click(object sender, EventArgs e) {

        }
    }
}