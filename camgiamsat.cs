using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Plate_recor_Final;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plate_recor_Final
{
    public class CamGiamSat : Form
    {
        private LibVLC _libVLC;
        private List<CameraControl> _cameraControls = new List<CameraControl>();
        private string connectionString = "Server=.;Database=QuanLyGiaoThong;Integrated Security=true;";

        // Modern color scheme
        private readonly Color bgDark = Color.FromArgb(15, 23, 42);
        private readonly Color cardBg = Color.FromArgb(30, 41, 59);
        private readonly Color accentBlue = Color.FromArgb(59, 130, 246);
        private readonly Color textPrimary = Color.FromArgb(248, 250, 252);
        private readonly Color textSecondary = Color.FromArgb(148, 163, 184);

        private PlateDetectionService _detectionService;

        public CamGiamSat()
        {
            InitializeComponent();
            InitializeLibVLC();
            SetupModernUI();
            LoadCameras();
            InitializeDetectionService();
        }

        // ==========================================
        // THÊM PHƯƠNG THỨC MỚI
        // ==========================================

        /// <summary>
        /// Khởi tạo detection service
        /// </summary>
        private void InitializeDetectionService()
        {
            try
            {
                _detectionService = PlateDetectionService.Instance;

                // Validate configuration
                if (!_detectionService.ValidateConfiguration(out string errorMessage))
                {
                    MessageBox.Show(
                        $"Cảnh báo: Không thể khởi tạo detection service\n\n{errorMessage}\n\n" +
                        "Vui lòng kiểm tra:\n" +
                        "1. Python đã được cài đặt\n" +
                        "2. File plate_detector.py tồn tại trong thư mục scripts\n" +
                        "3. Model YOLOv11 tồn tại trong thư mục models",
                        "Cảnh báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                else
                {
                    Console.WriteLine("Detection service initialized successfully");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khởi tạo detection service: {ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(1920, 1080);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.Text = "Giám sát Camera";
            this.ResumeLayout(false);
        }

        private ComboBox cboCamera;
        private ComboBox cboStatus;
        private TextBox txtSearch;

        private void SetupModernUI()
        {
            // Window Control Bar (Top right corner)
            Panel windowControlBar = new Panel
            {
                Location = new Point(this.Width - 180, 0),
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(30, 41, 59),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Close Button
            Button btnWinClose = new Button
            {
                Text = "✕",
                Location = new Point(140, 0),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnWinClose.FlatAppearance.BorderSize = 0;
            btnWinClose.MouseEnter += (s, e) => btnWinClose.BackColor = Color.FromArgb(220, 38, 38);
            btnWinClose.MouseLeave += (s, e) => btnWinClose.BackColor = Color.Transparent;
            btnWinClose.Click += (s, e) => this.Close();

            // Maximize/Restore Button
            Button btnWinMaximize = new Button
            {
                Text = "□",
                Location = new Point(100, 0),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnWinMaximize.FlatAppearance.BorderSize = 0;
            btnWinMaximize.MouseEnter += (s, e) => btnWinMaximize.BackColor = Color.FromArgb(51, 65, 85);
            btnWinMaximize.MouseLeave += (s, e) => btnWinMaximize.BackColor = Color.Transparent;
            btnWinMaximize.Click += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    this.WindowState = FormWindowState.Maximized;
                    btnWinMaximize.Text = "❐";
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                    btnWinMaximize.Text = "□";
                }
            };

            // Minimize Button
            Button btnWinMinimize = new Button
            {
                Text = "─",
                Location = new Point(60, 0),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnWinMinimize.FlatAppearance.BorderSize = 0;
            btnWinMinimize.MouseEnter += (s, e) => btnWinMinimize.BackColor = Color.FromArgb(51, 65, 85);
            btnWinMinimize.MouseLeave += (s, e) => btnWinMinimize.BackColor = Color.Transparent;
            btnWinMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            // Make control bar draggable
            windowControlBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            windowControlBar.Controls.AddRange(new Control[] { btnWinMinimize, btnWinMaximize, btnWinClose });

            // Left Sidebar Panel
            Panel sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = cardBg,
                Padding = new Padding(0)
            };

            // Sidebar Header
            Label sidebarTitle = new Label
            {
                Text = "LPR Control",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(20, 30),
                AutoSize = true
            };

            Label sidebarSubtitle = new Label
            {
                Text = "Giám sát & Nhận diện",
                Font = new Font("Segoe UI", 9),
                ForeColor = textSecondary,
                Location = new Point(20, 58),
                AutoSize = true
            };

            // Sidebar Menu Items
            Button btnGiamSat = CreateSidebarButton("📹  Giám sát", 100, true);
            Button btnPhanTich = CreateSidebarButton("📊  Phân tích giao thông", 160, false);
            Button btnTruyXuat = CreateSidebarButton("🔍  Truy xuất thông tin xe", 220, false);
            Button btnCaiDat = CreateSidebarButton("⚙️  Cài đặt", 280, false);

            btnPhanTich.Click += (s, e) =>
            {
                try
                {
                    PhanTichGiaoThong phanTichForm = new PhanTichGiaoThong();
                    phanTichForm.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở form giám sát: {ex.Message}");
                }
            };

            btnTruyXuat.Click += (s, e) =>
            {
                try
                {
                    TruySuatThongTinXe truyXuatForm = new TruySuatThongTinXe();
                    truyXuatForm.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở form giám sát: {ex.Message}");
                }
            };

            btnCaiDat.Click += (s, e) =>
            {
                try
                {
                    QuanLyCamera caiDatForm = new QuanLyCamera();
                    caiDatForm.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở form giám sát: {ex.Message}");
                }
            };


            sidebarPanel.Controls.AddRange(new Control[] {
                sidebarTitle, sidebarSubtitle,
                btnGiamSat, btnPhanTich, btnTruyXuat, btnCaiDat
            });

            // Header Panel
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = cardBg,
                Padding = new Padding(30, 0, 30, 0)
            };

            // Window Control Bar (Top right corner)
            Panel TopControlBar = new Panel
            {
                Location = new Point(this.Width - 180, 0),
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(30, 41, 59),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Close Button
            Button btnClose = new Button
            {
                Text = "✕",
                Location = new Point(140, 0),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.MouseEnter += (s, e) => btnClose.BackColor = Color.FromArgb(220, 38, 38);
            btnClose.MouseLeave += (s, e) => btnClose.BackColor = Color.Transparent;
            btnClose.Click += (s, e) => this.Close();

            // Maximize/Restore Button
            Button btnMaximize = new Button
            {
                Text = "□",
                Location = new Point(100, 0),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnMaximize.FlatAppearance.BorderSize = 0;
            btnMaximize.MouseEnter += (s, e) => btnMaximize.BackColor = Color.FromArgb(51, 65, 85);
            btnMaximize.MouseLeave += (s, e) => btnMaximize.BackColor = Color.Transparent;
            btnMaximize.Click += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    this.WindowState = FormWindowState.Maximized;
                    btnMaximize.Text = "❐";
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                    btnMaximize.Text = "□";
                }
            };

            // Minimize Button
            Button btnMinimize = new Button
            {
                Text = "─",
                Location = new Point(60, 0),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.MouseEnter += (s, e) => btnMinimize.BackColor = Color.FromArgb(51, 65, 85);
            btnMinimize.MouseLeave += (s, e) => btnMinimize.BackColor = Color.Transparent;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            // Make control bar draggable
            windowControlBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            windowControlBar.Controls.AddRange(new Control[] { btnMinimize, btnMaximize, btnClose });

            // Title Label
            Label titleLabel = new Label
            {
                Text = "Giám sát camera",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = textPrimary,
                AutoSize = true,
                Location = new Point(30, 25)
            };

            // Make header draggable
            headerPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    // Release capture and send WM_NCLBUTTONDOWN message
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            titleLabel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            // Toolbar Panel
            FlowLayoutPanel toolbarPanel = new FlowLayoutPanel
            {
                Location = new Point(350, 15),
                Size = new Size(850, 50),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Dropdown: Chọn camera
            cboCamera = CreateModernComboBox("Tất cả camera");
            LoadCameraNames(); // Load tên camera từ DB
            cboCamera.SelectedIndexChanged += CboCamera_SelectedIndexChanged;

            // Dropdown: Trạng thái
            cboStatus = CreateModernComboBox("Tất cả trạng thái");
            cboStatus.Items.AddRange(new object[] { "Tất cả trạng thái", "Active", "Inactive" });
            cboStatus.SelectedIndex = 0;
            cboStatus.SelectedIndexChanged += CboStatus_SelectedIndexChanged;

            // Button: Thêm camera
            Button btnAdd = CreateModernButton("Thêm camera", true);
            btnAdd.Click += BtnAdd_Click;

            // Button: Xem trạng thái detection
            Button btnViewStatus = CreateModernButton("Trạng thái Detection", false);
            btnViewStatus.Click += (s, e) =>
            {
                DetectionStatusForm statusForm = new DetectionStatusForm();
                statusForm.ShowDialog();
            };


            // TextBox: Tìm kiếm
            txtSearch = new TextBox
            {
                Width = 300,
                Height = 40,
                BackColor = bgDark,
                ForeColor = textSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Text = "Tìm theo tên camera/địa chỉ...",
                Margin = new Padding(10, 5, 0, 0),
                Padding = new Padding(10)
            };
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == "Tìm theo tên camera/địa chỉ...") txtSearch.Text = ""; };
            txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) txtSearch.Text = "Tìm theo tên camera/địa chỉ..."; };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            toolbarPanel.Controls.AddRange(new Control[] {
                cboCamera,
                cboStatus,
                btnAdd,
                btnViewStatus,  // ← THÊM DÒNG NÀY
                txtSearch
            });
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(toolbarPanel);

            // Main Content Panel with scroll
            FlowLayoutPanel contentPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(30, 20, 30, 20),
                BackColor = bgDark,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            contentPanel.Name = "contentPanel";

            this.Controls.Add(contentPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(windowControlBar);
        }

        private Button CreateSidebarButton(string text, int yPos, bool isActive)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(10, yPos),
                Size = new Size(230, 50),
                BackColor = isActive ? accentBlue : Color.Transparent,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            if (!isActive)
            {
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(51, 65, 85);
                btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
            }
            return btn;
        }

        // Windows API for dragging window
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void LoadCameraNames()
        {
            cboCamera.Items.Clear();
            cboCamera.Items.Add("Tất cả camera");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string query = "SELECT camera_id, camera_name FROM Cameras ORDER BY camera_name";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new CameraItem
                            {
                                CameraId = reader.GetInt32(0),
                                CameraName = reader.GetString(1)
                            };
                            cboCamera.Items.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            cboCamera.SelectedIndex = 0;
        }

        private void CboCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterCameras();
        }

        private void CboStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterCameras();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            FilterCameras();
        }

        private void FilterCameras()
        {
            FlowLayoutPanel contentPanel = this.Controls.Find("contentPanel", true)[0] as FlowLayoutPanel;
            contentPanel.Controls.Clear();
            _cameraControls.Clear();

            List<CameraInfo> cameras = GetCamerasFromDB();

            // Filter by selected camera
            if (cboCamera.SelectedItem is CameraItem selectedCamera)
            {
                cameras = cameras.Where(c => c.CameraId == selectedCamera.CameraId).ToList();
            }

            // Filter by status
            if (cboStatus.SelectedIndex > 0)
            {
                string status = cboStatus.SelectedItem.ToString();
                cameras = cameras.Where(c => c.Status == status).ToList();
            }

            // Filter by search text
            string searchText = txtSearch.Text;
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Tìm theo tên camera/địa chỉ...")
            {
                cameras = cameras.Where(c =>
                    c.CameraName.ToLower().Contains(searchText.ToLower()) ||
                    c.Location.ToLower().Contains(searchText.ToLower())
                ).ToList();
            }

            // Display filtered cameras
            foreach (var cam in cameras)
            {
                CameraControl camCtrl = new CameraControl(_libVLC, cam, cardBg, accentBlue, textPrimary, textSecondary);
                camCtrl.OnViewDetails += CamCtrl_OnViewDetails;
                camCtrl.OnTogglePlateDetection += CamCtrl_OnTogglePlateDetection;
                _cameraControls.Add(camCtrl);
                contentPanel.Controls.Add(camCtrl);
            }

            // Show message if no cameras found
            if (cameras.Count == 0)
            {
                Label lblNoData = new Label
                {
                    Text = "Không tìm thấy camera nào",
                    Font = new Font("Segoe UI", 14),
                    ForeColor = textSecondary,
                    AutoSize = true,
                    Location = new Point(50, 50)
                };
                contentPanel.Controls.Add(lblNoData);
            }
        }

        private ComboBox CreateModernComboBox(string defaultText)
        {
            ComboBox cbo = new ComboBox
            {
                Width = 180,
                Height = 40,
                BackColor = bgDark,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 5, 15, 0)
            };
            return cbo;
        }

        private Button CreateModernButton(string text, bool isPrimary)
        {
            Button btn = new Button
            {
                Text = text,
                Width = 150,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = isPrimary ? accentBlue : cardBg,
                ForeColor = textPrimary,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 5, 15, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = isPrimary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(51, 65, 85);
            btn.MouseLeave += (s, e) => btn.BackColor = isPrimary ? accentBlue : cardBg;
            return btn;
        }

        private void InitializeLibVLC()
        {
            Core.Initialize();
            _libVLC = new LibVLC();
        }

        //private void LoadCameras()
        //{
        //    FilterCameras();
        //}

        // ==========================================
        // CẬP NHẬT PHƯƠNG THỨC LoadCameras
        // ==========================================

        private void LoadCameras()
        {
            FilterCameras();

            // Tự động bật detection cho các camera đã được enable
            Task.Run(() =>
            {
                try
                {
                    List<CameraInfo> cameras = GetCamerasFromDB();
                    foreach (var camera in cameras.Where(c => c.EnablePlateDetection && c.Status == "Active"))
                    {
                        this.Invoke(new Action(() =>
                        {
                            // ← THÊM DÒNG NÀY: Kiểm tra đã chạy chưa
                            bool alreadyRunning = _detectionService.IsDetectionRunning(camera.CameraId);
                            if (!alreadyRunning)
                            {
                                _detectionService.StartDetection(camera.CameraId, camera.CameraUrl, out string error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Console.WriteLine($"Failed to auto-start detection for camera {camera.CameraId}: {error}");
                                }
                                else
                                {
                                    Console.WriteLine($"Auto-started detection for camera {camera.CameraId}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Detection already running for camera {camera.CameraId}");
                            }
                        }));

                        System.Threading.Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error auto-starting detections: {ex.Message}");
                }
            });
        }


        private List<CameraInfo> GetCamerasFromDB()
        {
            List<CameraInfo> cameras = new List<CameraInfo>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT camera_id, camera_name, camera_url, location_address, status, enable_plate_detection FROM Cameras";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cameras.Add(new CameraInfo
                        {
                            CameraId = reader.GetInt32(0),
                            CameraName = reader.GetString(1),
                            CameraUrl = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Location = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Status = reader.GetString(4),
                            EnablePlateDetection = reader.GetBoolean(5)
                        });
                    }
                }
            }
            return cameras;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            //// Navigate to Settings page
            //this.Hide();
            //CaiDat settingsForm = new CaiDat();
            //settingsForm.FormClosed += (s, args) => this.Show();
            //settingsForm.Show();
        }

        private void CamCtrl_OnViewDetails(CameraInfo camera)
        {
            // Show camera stream in fullscreen with sidebar
            ShowCameraDetailView(camera);
        }

        private void ShowCameraDetailView(CameraInfo camera)
        {
            // Clear content panel
            FlowLayoutPanel contentPanel = this.Controls.Find("contentPanel", true)[0] as FlowLayoutPanel;
            contentPanel.Controls.Clear();

            // Create back button panel
            Panel backPanel = new Panel
            {
                Width = contentPanel.Width - 60,
                Height = 50,
                BackColor = Color.Transparent,
                Location = new Point(0, 0)
            };

            Button btnBack = new Button
            {
                Text = "← Quay lại",
                Width = 150,
                Height = 40,
                BackColor = cardBg,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(0, 5)
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => LoadCameras(); // Reload camera grid
            backPanel.Controls.Add(btnBack);

            // Create large video panel
            Panel videoPanel = new Panel
            {
                Width = contentPanel.Width - 60,
                Height = contentPanel.Height - 150,
                BackColor = Color.Black,
                Location = new Point(0, 60)
            };

            // Camera info header
            Label lblCameraName = new Label
            {
                Text = camera.CameraName,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = textPrimary,
                AutoSize = true,
                Location = new Point(15, 15),
                BackColor = Color.FromArgb(150, 0, 0, 0),
                Padding = new Padding(10)
            };

            Label lblLocation = new Label
            {
                Text = camera.Location,
                Font = new Font("Segoe UI", 12),
                ForeColor = textSecondary,
                AutoSize = true,
                Location = new Point(15, 55),
                BackColor = Color.FromArgb(150, 0, 0, 0),
                Padding = new Padding(10)
            };

            Label lblStatus = new Label
            {
                Text = camera.Status,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = camera.Status == "Active" ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68),
                AutoSize = true,
                Location = new Point(videoPanel.Width - 100, 15),
                BackColor = camera.Status == "Active" ? Color.FromArgb(22, 101, 52) : Color.FromArgb(127, 29, 29),
                Padding = new Padding(10, 5, 10, 5)
            };

            // YOLOv8 Badge
            Label lblYolo = new Label
            {
                Text = "YOLOv8n-plate Detection: " + (camera.EnablePlateDetection ? "ON" : "OFF"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = textPrimary,
                BackColor = camera.EnablePlateDetection ? accentBlue : Color.FromArgb(100, 100, 100),
                AutoSize = true,
                Location = new Point(15, 95),
                Padding = new Padding(10, 5, 10, 5)
            };

            // Create VideoView for streaming
            VideoView detailVideoView = new VideoView
            {
                Location = new Point(0, 0),
                Size = videoPanel.Size,
                BackColor = Color.Black,
                Dock = DockStyle.Fill
            };

            videoPanel.Controls.AddRange(new Control[] { detailVideoView, lblCameraName, lblLocation, lblStatus, lblYolo });
            lblCameraName.BringToFront();
            lblLocation.BringToFront();
            lblStatus.BringToFront();
            lblYolo.BringToFront();

            // Control buttons panel
            FlowLayoutPanel controlPanel = new FlowLayoutPanel
            {
                Width = contentPanel.Width - 60,
                Height = 60,
                Location = new Point(0, videoPanel.Height + 70),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            Button btnToggleDetection = new Button
            {
                Text = camera.EnablePlateDetection ? "Tắt nhận diện biển số" : "Bật nhận diện biển số",
                Width = 200,
                Height = 45,
                BackColor = camera.EnablePlateDetection ? Color.FromArgb(239, 68, 68) : accentBlue,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 5, 15, 0)
            };
            btnToggleDetection.FlatAppearance.BorderSize = 0;
            btnToggleDetection.Click += (s, e) =>
            {
                camera.EnablePlateDetection = !camera.EnablePlateDetection;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE Cameras SET enable_plate_detection = @enabled WHERE camera_id = @id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@enabled", camera.EnablePlateDetection);
                        cmd.Parameters.AddWithValue("@id", camera.CameraId);
                        cmd.ExecuteNonQuery();
                    }
                }
                btnToggleDetection.Text = camera.EnablePlateDetection ? "Tắt nhận diện biển số" : "Bật nhận diện biển số";
                btnToggleDetection.BackColor = camera.EnablePlateDetection ? Color.FromArgb(239, 68, 68) : accentBlue;
                lblYolo.Text = "YOLOv8n-plate Detection: " + (camera.EnablePlateDetection ? "ON" : "OFF");
                lblYolo.BackColor = camera.EnablePlateDetection ? accentBlue : Color.FromArgb(100, 100, 100);
            };

            btnToggleDetection.Click += (s, e) =>
            {
                camera.EnablePlateDetection = !camera.EnablePlateDetection;

                // Cập nhật database
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE Cameras SET enable_plate_detection = @enabled WHERE camera_id = @id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@enabled", camera.EnablePlateDetection);
                        cmd.Parameters.AddWithValue("@id", camera.CameraId);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Bật/tắt detection service
                string errorMessage;
                if (camera.EnablePlateDetection)
                {
                    bool success = _detectionService.StartDetection(camera.CameraId, camera.CameraUrl, out errorMessage);
                    if (!success)
                    {
                        MessageBox.Show($"Lỗi: {errorMessage}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        camera.EnablePlateDetection = false; // Rollback
                        return;
                    }
                }
                else
                {
                    bool success = _detectionService.StopDetection(camera.CameraId, out errorMessage);
                    if (!success)
                    {
                        MessageBox.Show($"Lỗi: {errorMessage}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // Cập nhật UI
                btnToggleDetection.Text = camera.EnablePlateDetection ? "Tắt nhận diện biển số" : "Bật nhận diện biển số";
                btnToggleDetection.BackColor = camera.EnablePlateDetection ? Color.FromArgb(239, 68, 68) : accentBlue;
                lblYolo.Text = "YOLOv8n-plate Detection: " + (camera.EnablePlateDetection ? "ON" : "OFF");
                lblYolo.BackColor = camera.EnablePlateDetection ? accentBlue : Color.FromArgb(100, 100, 100);
            };

            controlPanel.Controls.AddRange(new Control[] { btnToggleDetection});

            contentPanel.Controls.AddRange(new Control[] { backPanel, videoPanel, controlPanel });

            // Start streaming
            if (camera.Status == "Active" && !string.IsNullOrEmpty(camera.CameraUrl))
            {
                try
                {
                    var mediaPlayer = new MediaPlayer(_libVLC);
                    detailVideoView.MediaPlayer = mediaPlayer;

                    var media = new Media(_libVLC, camera.CameraUrl, FromType.FromLocation);
                    media.AddOption(":network-caching=300");
                    media.AddOption(":clock-jitter=0");
                    media.AddOption(":clock-synchro=0");

                    mediaPlayer.Play(media);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi kết nối camera: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        //private void CamCtrl_OnTogglePlateDetection(CameraInfo camera, bool enabled)
        //{
        //    using (SqlConnection conn = new SqlConnection(connectionString))
        //    {
        //        conn.Open();
        //        string query = "UPDATE Cameras SET enable_plate_detection = @enabled WHERE camera_id = @id";
        //        using (SqlCommand cmd = new SqlCommand(query, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@enabled", enabled);
        //            cmd.Parameters.AddWithValue("@id", camera.CameraId);
        //            cmd.ExecuteNonQuery();
        //        }
        //    }
        //}

        // ==========================================
        // CẬP NHẬT PHƯƠNG THỨC CamCtrl_OnTogglePlateDetection
        // ==========================================
        private void CamCtrl_OnTogglePlateDetection(CameraInfo camera, bool enabled)
        {
            // Cập nhật database
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "UPDATE Cameras SET enable_plate_detection = @enabled WHERE camera_id = @id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@enabled", enabled);
                    cmd.Parameters.AddWithValue("@id", camera.CameraId);
                    cmd.ExecuteNonQuery();
                }
            }

            // Bật/tắt detection service
            string errorMessage;
            bool success;

            if (enabled)
            {
                // Bắt đầu detection
                success = _detectionService.StartDetection(camera.CameraId, camera.CameraUrl, out errorMessage);
                if (success)
                {
                    MessageBox.Show(
                        $"Đã bật nhận diện biển số cho camera:\n{camera.CameraName}",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Lỗi khi bật nhận diện:\n{errorMessage}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            else
            {
                // Dừng detection
                success = _detectionService.StopDetection(camera.CameraId, out errorMessage);
                if (success)
                {
                    MessageBox.Show(
                        $"Đã tắt nhận diện biển số cho camera:\n{camera.CameraName}",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Lỗi khi tắt nhận diện:\n{errorMessage}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }


        //protected override void OnFormClosing(FormClosingEventArgs e)
        //{
        //    foreach (var ctrl in _cameraControls)
        //    {
        //        ctrl.Dispose();
        //    }
        //    _libVLC?.Dispose();
        //    base.OnFormClosing(e);
        //}

        // ==========================================
        // CẬP NHẬT PHƯƠNG THỨC OnFormClosing
        // ==========================================
        /// <summary> 
        /// QUAN TRỌNG: Chỉ cleanup UI resources, KHÔNG dừng detection
        /// Detection sẽ tiếp tục chạy cho đến khi thoát ứng dụng hoàn toàn
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // KHÔNG hỏi xác nhận
            // KHÔNG dừng detection
            // Detection service chạy global, độc lập với form

            Console.WriteLine("[CamGiamSat] Form closing - Detection continues running");

            // Chỉ cleanup UI resources
            foreach (var ctrl in _cameraControls)
            {
                ctrl.Dispose();
            }

            _libVLC?.Dispose();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Method tĩnh để thoát ứng dụng với xác nhận
        /// Gọi từ main form hoặc nút Exit
        /// </summary>
        public static void ExitApplicationWithConfirmation()
        {
            try
            {
                var service = PlateDetectionService.Instance;
                var runningCameras = service.GetRunningCameras();

                if (runningCameras.Count > 0)
                {
                    DialogResult result = MessageBox.Show(
                        $"Có {runningCameras.Count} camera đang chạy detection.\n\n" +
                        "Nếu thoát, tất cả detection sẽ bị dừng.\n\n" +
                        "Bạn có chắc muốn thoát?",
                        "Xác nhận thoát ứng dụng",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2
                    );

                    if (result == DialogResult.Yes)
                    {
                        Console.WriteLine("[EXIT] Stopping all detections...");
                        service.StopAllDetections();
                        Application.Exit();
                    }
                }
                else
                {
                    // Không có detection, thoát luôn
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khi thoát: {ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

}

    // Camera Info Model
    public class CameraInfo
    {
        public int CameraId { get; set; }
        public string CameraName { get; set; }
        public string CameraUrl { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public bool EnablePlateDetection { get; set; }
    }

    // Camera Item for ComboBox
    public class CameraItem
    {
        public int CameraId { get; set; }
        public string CameraName { get; set; }

        public override string ToString()
        {
            return CameraName;
        }
    }

    // Custom Camera Control
    public class CameraControl : Panel
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoView _videoView;
        private CameraInfo _camera;
        private Label _fpsLabel;
        private System.Windows.Forms.Timer _fpsTimer;
        private int _frameCount = 0;

        public event Action<CameraInfo> OnViewDetails;
        public event Action<CameraInfo, bool> OnTogglePlateDetection;
        
        private PlateDetectionService _detectionService;

        public CameraControl(LibVLC libVLC, CameraInfo camera, Color cardBg, Color accentBlue, Color textPrimary, Color textSecondary)
        {
            _libVLC = libVLC;
            _camera = camera;
            _detectionService = PlateDetectionService.Instance;

            this.Size = new Size(440, 380);
            this.BackColor = cardBg;
            this.Margin = new Padding(10);
            this.Padding = new Padding(15);

            // Header
            Label lblName = new Label
            {
                Text = camera.CameraName,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = textPrimary,
                AutoSize = true,
                Location = new Point(15, 15)
            };

            Label lblStatus = new Label
            {
                Text = camera.Status,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = camera.Status == "Active" ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68),
                AutoSize = true,
                Location = new Point(380, 18),
                BackColor = camera.Status == "Active" ? Color.FromArgb(22, 101, 52) : Color.FromArgb(127, 29, 29),
                Padding = new Padding(8, 4, 8, 4)
            };

            Label lblLocation = new Label
            {
                Text = camera.Location,
                Font = new Font("Segoe UI", 9),
                ForeColor = textSecondary,
                AutoSize = true,
                Location = new Point(15, 40)
            };

            // Video View
            _videoView = new VideoView
            {
                Location = new Point(15, 70),
                Size = new Size(410, 230),
                BackColor = Color.Black
            };

            // YOLOv8 Badge
            Label lblYolo = new Label
            {
                Text = "YOLOv8n-plate",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = textPrimary,
                BackColor = accentBlue,
                AutoSize = true,
                Location = new Point(25, 80),
                Padding = new Padding(8, 4, 8, 4)
            };
            lblYolo.BringToFront();

            // FPS Label
            _fpsLabel = new Label
            {
                Text = "FPS: 0",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = textPrimary,
                BackColor = Color.FromArgb(100, 0, 0, 0),
                AutoSize = true,
                Location = new Point(350, 80),
                Padding = new Padding(8, 4, 8, 4)
            };
            _fpsLabel.BringToFront();

            // Buttons
            Button btnDetails = new Button
            {
                Text = "Xem chi tiết",
                Location = new Point(15, 315),
                Size = new Size(200, 40),
                BackColor = accentBlue,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDetails.FlatAppearance.BorderSize = 0;
            btnDetails.Click += (s, e) => OnViewDetails?.Invoke(_camera);

            Button btnToggle = new Button
            {
                Text = "Nhận diện xe",
                Location = new Point(225, 315),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Click += (s, e) => {
            _camera.EnablePlateDetection = !_camera.EnablePlateDetection;
            OnTogglePlateDetection?.Invoke(_camera, _camera.EnablePlateDetection);

            // Cập nhật màu button
            bool isRunning = _detectionService.IsDetectionRunning(_camera.CameraId);
            btnToggle.BackColor = isRunning ? accentBlue : Color.FromArgb(51, 65, 85);
            };

            bool isCurrentlyRunning = _detectionService.IsDetectionRunning(_camera.CameraId);
            btnToggle.BackColor = isCurrentlyRunning ? accentBlue : Color.FromArgb(51, 65, 85);

            this.Controls.AddRange(new Control[] { lblName, lblStatus, lblLocation, _videoView, lblYolo, _fpsLabel, btnDetails, btnToggle });

            // Start streaming
            if (camera.Status == "Active" && !string.IsNullOrEmpty(camera.CameraUrl))
            {
                StartStreaming(camera.CameraUrl);
            }

            // FPS Timer
            _fpsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _fpsTimer.Tick += (s, e) => {
                _fpsLabel.Text = $"FPS: {_frameCount}";
                _frameCount = 0;
            };
            _fpsTimer.Start();
        }

        private void StartStreaming(string url)
        {
            try
            {
                _mediaPlayer = new MediaPlayer(_libVLC);
                _videoView.MediaPlayer = _mediaPlayer;

                var media = new Media(_libVLC, url, FromType.FromLocation);
                media.AddOption(":network-caching=300");
                media.AddOption(":clock-jitter=0");
                media.AddOption(":clock-synchro=0");

                _mediaPlayer.Play(media);

                // Simulate FPS counter
                System.Windows.Forms.Timer fpsSimulator = new System.Windows.Forms.Timer { Interval = 40 };
                fpsSimulator.Tick += (s, e) => _frameCount++;
                fpsSimulator.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối camera: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Không tự động dừng detection khi dispose control
                // Vì có thể nhiều control cùng quản lý 1 camera

                _fpsTimer?.Stop();
                _fpsTimer?.Dispose();
                _mediaPlayer?.Stop();
                _mediaPlayer?.Dispose();
            }
            base.Dispose(disposing);
        }
}



