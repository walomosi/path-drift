using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Windows.Forms;

namespace PathDrift
{
    public class IpSeries
    {
        public string Ip;
        public List<double> Samples = new List<double>();
        public Color Color;
        public double Avg;
        public double Max;
        public double Jitter;
        public int Timeouts;
    }

    public class MainForm : Form
    {
        private TextBox _txtIps;
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnReadHosts;
        private Button _btnExport;
        private CheckBox _chkHttp;
        private NumericUpDown _numInterval;
        private NumericUpDown _numAlarm;
        private Panel _canvas;
        private System.Threading.Timer _timer;
        private List<IpSeries> _series = new List<IpSeries>();
        private int _tickCount;
        private bool _warpDetected;
        private static readonly Color[] Palette = { Color.Firebrick, Color.SteelBlue, Color.ForestGreen, Color.DarkOrange, Color.Purple };
        private const int MaxPoints = 300;
        private const int DrawEvery = 3; // repaint every N ticks to save CPU

        public MainForm()
        {
            Text = "Path Drift - monitor your accelerator IPs";
            Width = 920;
            Height = 660;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            Label l1 = new Label();
            l1.Text = "IPs (one per line):";
            l1.Top = 12; l1.Left = 12; l1.Width = 110;
            Controls.Add(l1);

            _txtIps = new TextBox();
            _txtIps.Multiline = true;
            _txtIps.Top = 12; _txtIps.Left = 125;
            _txtIps.Width = 180; _txtIps.Height = 80;
            _txtIps.Text = "1.1.1.1\n8.8.8.8";
            Controls.Add(_txtIps);

            _btnReadHosts = new Button();
            _btnReadHosts.Text = "Read hosts";
            _btnReadHosts.Top = 100; _btnReadHosts.Left = 125;
            _btnReadHosts.Width = 85; _btnReadHosts.Height = 26;
            _btnReadHosts.Click += OnReadHosts;
            Controls.Add(_btnReadHosts);

            _btnExport = new Button();
            _btnExport.Text = "Export best";
            _btnExport.Top = 100; _btnExport.Left = 215;
            _btnExport.Width = 85; _btnExport.Height = 26;
            _btnExport.Click += OnExport;
            Controls.Add(_btnExport);

            Button btnCsv = new Button();
            btnCsv.Text = "Export CSV";
            btnCsv.Top = 130; btnCsv.Left = 125;
            btnCsv.Width = 85; btnCsv.Height = 26;
            btnCsv.Click += OnExportCsv;
            Controls.Add(btnCsv);

            Label l2 = new Label();
            l2.Text = "interval (s):";
            l2.Top = 12; l2.Left = 320; l2.Width = 70;
            Controls.Add(l2);

            _numInterval = new NumericUpDown();
            _numInterval.Top = 10; _numInterval.Left = 395;
            _numInterval.Width = 55;
            _numInterval.Minimum = 1; _numInterval.Maximum = 300;
            _numInterval.Value = 5;
            Controls.Add(_numInterval);

            Label l3 = new Label();
            l3.Text = "alarm >ms:";
            l3.Top = 40; l3.Left = 320; l3.Width = 70;
            Controls.Add(l3);

            _numAlarm = new NumericUpDown();
            _numAlarm.Top = 38; _numAlarm.Left = 395;
            _numAlarm.Width = 55;
            _numAlarm.Minimum = 0; _numAlarm.Maximum = 2000;
            _numAlarm.Value = 200;
            Controls.Add(_numAlarm);

            _chkHttp = new CheckBox();
            _chkHttp.Text = "HTTP mode (for ICMP-blocked IPs)";
            _chkHttp.Top = 70; _chkHttp.Left = 320;
            _chkHttp.Width = 230;
            Controls.Add(_chkHttp);

            _btnStart = new Button();
            _btnStart.Text = "Start";
            _btnStart.Top = 10; _btnStart.Left = 600;
            _btnStart.Width = 90; _btnStart.Height = 30;
            _btnStart.BackColor = Color.LightGreen;
            _btnStart.Click += OnStart;
            Controls.Add(_btnStart);

            _btnStop = new Button();
            _btnStop.Text = "Stop";
            _btnStop.Top = 48; _btnStop.Left = 600;
            _btnStop.Width = 90; _btnStop.Height = 30;
            _btnStop.Click += OnStop;
            Controls.Add(_btnStop);

            _canvas = new Panel();
            _canvas.Top = 140; _canvas.Left = 12;
            _canvas.Width = 880; _canvas.Height = 480;
            _canvas.BackColor = Color.White;
            _canvas.BorderStyle = BorderStyle.FixedSingle;
            _canvas.Paint += OnPaint;
            Controls.Add(_canvas);

            _warpDetected = DetectWarp();
            if (_warpDetected)
            {
                MessageBox.Show(this,
                    "Cloudflare WARP detected!\n\n" +
                    "WARP routes all traffic through Cloudflare's network, which will:\n" +
                    "  - Add 20-100ms baseline latency to every IP\n" +
                    "  - Mask the real path to your accelerator IPs\n" +
                    "  - Make drift curves look flatter than reality\n\n" +
                    "Disconnect WARP before measuring real path stability.",
                    "WARP detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool DetectWarp()
        {
            try
            {
                // check service
                using (ServiceController sc = new ServiceController("CloudflareWARP"))
                {
                    if (sc.Status == ServiceControllerStatus.Running) return true;
                }
            }
            catch { }
            try
            {
                foreach (Process p in Process.GetProcessesByName("warp-cli")) return true;
                foreach (Process p in Process.GetProcessesByName("cloudflare-warp")) return true;
            }
            catch { }
            return false;
        }

        private void OnReadHosts(object sender, EventArgs e)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                var ips = new List<string>();
                foreach (string line in File.ReadAllLines(path))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    string[] parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        IPAddress ip;
                        if (IPAddress.TryParse(parts[0], out ip) && !ips.Contains(parts[0]))
                            ips.Add(parts[0]);
                    }
                }
                _txtIps.Lines = ips.ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to read hosts: " + ex.Message);
            }
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (_series.Count == 0) { MessageBox.Show("No data yet."); return; }
            IpSeries best = null;
            foreach (IpSeries s in _series)
            {
                if (s.Samples.Count < 5) continue;
                if (best == null || s.Avg < best.Avg) best = s;
            }
            if (best == null) { MessageBox.Show("Not enough samples."); return; }
            Clipboard.SetText(best.Ip);
            MessageBox.Show(this, "Best IP copied to clipboard:\n" + best.Ip + "  avg " + (int)best.Avg + "ms");
        }

        private void OnExportCsv(object sender, EventArgs e)
        {
            if (_series.Count == 0) { MessageBox.Show("No data yet."); return; }
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV|*.csv";
                sfd.FileName = "pathdrift-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv";
                if (sfd.ShowDialog() != DialogResult.OK) return;
                using (StreamWriter sw = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(false)))
                {
                    sw.WriteLine("timestamp,ip,latency_ms");
                    foreach (IpSeries s in _series)
                    {
                        lock (s.Samples)
                        {
                            for (int i = 0; i < s.Samples.Count; i++)
                            {
                                sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "," + s.Ip + "," + s.Samples[i]);
                            }
                        }
                    }
                }
                MessageBox.Show(this, "Exported to " + sfd.FileName);
            }
        }

        private void OnStart(object sender, EventArgs e)
        {
            string[] ips = _txtIps.Lines;
            _series.Clear();
            int ci = 0;
            foreach (string line in ips)
            {
                string ip = line.Trim();
                if (ip.Length == 0) continue;
                _series.Add(new IpSeries { Ip = ip, Color = Palette[ci % Palette.Length] });
                ci++;
            }
            int ms = (int)_numInterval.Value * 1000;
            _timer = new System.Threading.Timer(OnTick, null, 0, ms);
            _btnStart.Enabled = false;
        }

        private void OnStop(object sender, EventArgs e)
        {
            if (_timer != null) { _timer.Dispose(); _timer = null; }
            _btnStart.Enabled = true;
        }

        // HTTP mode exists because many accelerator IPs block ICMP at the edge;
        // a HEAD request on port 80 still measures real path latency.
        private void OnTick(object state)
        {
            bool http = _chkHttp.Checked;
            double alarm = (double)_numAlarm.Value;
            foreach (IpSeries s in _series)
            {
                double ms = -1;
                try
                {
                    if (http)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://" + s.Ip + "/");
                        req.Timeout = 2000;
                        req.Method = "HEAD";
                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) { }
                        sw.Stop();
                        ms = sw.Elapsed.TotalMilliseconds;
                    }
                    else
                    {
                        using (Ping p = new Ping())
                        {
                            PingReply r = p.Send(s.Ip, 2000);
                            if (r.Status == IPStatus.Success) ms = r.RoundtripTime;
                        }
                    }
                }
                catch { ms = -1; }

                lock (s.Samples)
                {
                    s.Samples.Add(ms);
                    if (s.Samples.Count > MaxPoints) s.Samples.RemoveAt(0);
                    if (ms < 0) s.Timeouts++;
                    ComputeStats(s);
                }

                if (alarm > 0 && ms > alarm)
                {
                    try
                    {
                        _canvas.BeginInvoke(new Action(() =>
                        {
                            Text = "Path Drift - ALARM: " + s.Ip + " = " + (int)ms + "ms";
                        }));
                    }
                    catch { }
                }
            }
            _tickCount++;
            if (_tickCount % DrawEvery == 0 && _canvas.IsHandleCreated)
            {
                try { _canvas.BeginInvoke(new Action(() => _canvas.Invalidate())); }
                catch { }
            }
        }

        private void ComputeStats(IpSeries s)
        {
            int n = 0; double sum = 0, max = 0, prev = -1, jitter = 0;
            foreach (double v in s.Samples)
            {
                if (v < 0) continue;
                n++; sum += v;
                if (v > max) max = v;
                if (prev >= 0) jitter += Math.Abs(v - prev);
                prev = v;
            }
            if (n > 0)
            {
                s.Avg = sum / n;
                s.Max = max;
                s.Jitter = (n > 1) ? jitter / (n - 1) : 0;
            }
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_series.Count == 0)
            {
                g.DrawString("Enter IPs and click Start.", new Font("Segoe UI", 11), Brushes.Gray, 380, 220);
                return;
            }

            int padL = 50, padR = 200, padT = 20, padB = 40;
            int w = _canvas.Width - padL - padR;
            int h = _canvas.Height - padT - padB;
            double ymax = 500;

            for (int i = 0; i <= 5; i++)
            {
                int y = padT + h - (int)(h * i / 5.0);
                g.DrawLine(Pens.LightGray, padL, y, padL + w, y);
                g.DrawString((i * 100) + "ms", new Font("Segoe UI", 8), Brushes.Gray, 5, y - 6);
            }

            foreach (IpSeries s in _series)
            {
                lock (s.Samples)
                {
                    if (s.Samples.Count < 2) continue;
                    using (Pen pen = new Pen(s.Color, 2))
                    {
                        Point prev = new Point(0, 0);
                        for (int i = 0; i < s.Samples.Count; i++)
                        {
                            double v = s.Samples[i];
                            int x = padL + (int)(w * i / (double)(MaxPoints - 1));
                            int y = padT + h - (int)(h * Math.Min(v, ymax) / ymax);
                            if (v < 0) y = padT + h;
                            Point pt = new Point(x, y);
                            if (i > 0) g.DrawLine(pen, prev, pt);
                            prev = pt;
                        }
                    }
                }

                int ly = padT + _series.IndexOf(s) * 70;
                using (Pen pen = new Pen(s.Color, 3))
                {
                    g.DrawLine(pen, padL + w + 10, ly + 8, padL + w + 30, ly + 8);
                }
                g.DrawString(s.Ip, new Font("Segoe UI", 9.5f, FontStyle.Bold), Brushes.Black, padL + w + 35, ly);
                g.DrawString("avg " + (int)s.Avg + "ms  max " + (int)s.Max + "ms", new Font("Segoe UI", 8.5f), Brushes.DimGray, padL + w + 35, ly + 18);
                g.DrawString("jitter " + (int)s.Jitter + "ms  timeouts " + s.Timeouts, new Font("Segoe UI", 8.5f), Brushes.DimGray, padL + w + 35, ly + 34);
            }
        }
    }
}
