using System;
using System.Drawing;
using System.Windows.Forms;

namespace Plate_recor_Final
{
    public class DetectionStatusForm : Form
    {
        private FlowLayoutPanel panelStatus;
        private Timer refreshTimer;
        private PlateDetectionService _service;
        private Label lblHeader;
        private Button btnRefresh;
        private Button btnStopAll;
        private Button btnClose;

        public DetectionStatusForm()
        {
            InitializeComponent();
            _service = PlateDetectionService.Instance;
            LoadStatus();

            // Auto refresh mỗi 2 giây
            refreshTimer = new Timer { Interval = 2000 };
            refreshTimer.Tick += (s, e) => LoadStatus();
            refreshTimer.Start();
        }

        private void InitializeComponent()
        {
            this.Text = "Trạng thái Detection";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(15, 23, 42);

            // Header
            lblHeader = new Label
            {
                Text = "Camera đang chạy Detection",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 250, 252),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            // Status Panel
            panelStatus = new FlowLayoutPanel
            {
                Location = new Point(20, 60),
                Size = new Size(440, 280),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Buttons
            btnRefresh = new Button
            {
                Text = "Làm mới",
                Location = new Point(20, 360),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadStatus();

            btnStopAll = new Button
            {
                Text = "Dừng tất cả",
                Location = new Point(150, 360),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStopAll.FlatAppearance.BorderSize = 0;
            btnStopAll.Click += BtnStopAll_Click;

            btnClose = new Button
            {
                Text = "✕ Đóng",
                Location = new Point(340, 360),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblHeader, panelStatus, btnRefresh, btnStopAll, btnClose
            });
        }

        private void LoadStatus()
        {
            panelStatus.Controls.Clear();

            var runningCameras = _service.GetRunningCameras();

            if (runningCameras.Count == 0)
            {
                Label lblEmpty = new Label
                {
                    Text = "Không có detection nào đang chạy",
                    Font = new Font("Segoe UI", 11),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    AutoSize = true,
                    Padding = new Padding(20)
                };
                panelStatus.Controls.Add(lblEmpty);
                return;
            }

            lblHeader.Text = $"{runningCameras.Count} Camera đang chạy Detection";

            foreach (var cameraId in runningCameras)
            {
                Panel itemPanel = new Panel
                {
                    Size = new Size(420, 70),
                    BackColor = Color.FromArgb(51, 65, 85),
                    Margin = new Padding(5),
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblCameraId = new Label
                {
                    Text = $" Camera ID: {cameraId}",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.FromArgb(248, 250, 252),
                    Location = new Point(10, 10),
                    AutoSize = true
                };

                string info = _service.GetDetectionInfo(cameraId);
                Label lblInfo = new Label
                {
                    Text = info.Replace("\n", " | "),
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    Location = new Point(10, 35),
                    Size = new Size(300, 25),
                    AutoEllipsis = true
                };

                Button btnStop = new Button
                {
                    Text = " Dừng",
                    Location = new Point(320, 15),
                    Size = new Size(85, 35),
                    BackColor = Color.FromArgb(239, 68, 68),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Tag = cameraId
                };
                btnStop.FlatAppearance.BorderSize = 0;
                btnStop.Click += BtnStop_Click;

                itemPanel.Controls.AddRange(new Control[] { lblCameraId, lblInfo, btnStop });
                panelStatus.Controls.Add(itemPanel);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            int cameraId = (int)btn.Tag;

            DialogResult result = MessageBox.Show(
                $"Dừng detection cho Camera {cameraId}?",
                "Xác nhận",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                bool success = _service.StopDetection(cameraId, out string error);
                if (success)
                {
                    MessageBox.Show(
                        "Đã dừng detection",
                        "Thành công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    LoadStatus();
                }
                else
                {
                    MessageBox.Show(
                        $"",
                        "",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        private void BtnStopAll_Click(object sender, EventArgs e)
        {
            var runningCameras = _service.GetRunningCameras();
            if (runningCameras.Count == 0)
            {
                MessageBox.Show(
                    "Không có detection nào đang chạy",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            DialogResult result = MessageBox.Show(
                $"Dừng tất cả {runningCameras.Count} detection đang chạy?",
                "Xác nhận",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                _service.StopAllDetections();
                MessageBox.Show(
                    "Đã dừng tất cả detection",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                LoadStatus();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}