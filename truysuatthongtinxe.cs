using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plate_recor_Final
{
    public partial class TruySuatThongTinXe : Form
    {
        [DllImport("user32.DLL", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();
        [DllImport("user32.DLL", EntryPoint = "SendMessage")]
        private extern static void SendMessage(System.IntPtr hWnd, int wMsg, int wParam, int lParam);

        // Modern UI Colors
        private readonly Color bgDark = Color.FromArgb(15, 23, 42);
        private readonly Color cardBg = Color.FromArgb(30, 41, 59);
        private readonly Color textPrimary = Color.FromArgb(248, 250, 252);
        private readonly Color textSecondary = Color.FromArgb(148, 163, 184);
        private readonly Color accentBlue = Color.FromArgb(59, 130, 246);
        private readonly Color successGreen = Color.FromArgb(34, 197, 94);
        private readonly Color errorRed = Color.FromArgb(239, 68, 68);

        // Database connection
        private string connectionString = @"Server=HOANGLAM\LAM;Database=QuanLyGiaoThong;User Id=sa;Password=2211;Encrypt=True;TrustServerCertificate=True;";

        // Filter controls
        private DateTimePicker dtpFromDate;
        private DateTimePicker dtpToDate;
        private CheckBox cbEnableDateFilter;
        private RadioButton rbXeMay;
        private RadioButton rbXeHoi;
        private RadioButton rbXeBuyt;
        private RadioButton rbXeTai;
        private RadioButton rbTatCa;
        private TextBox txtSearchPlate;
        private Button btnSearch;
        private Button btnReset;
        private DataGridView dgvVehicles;

        public TruySuatThongTinXe()
        {
            InitializeComponent();
            SetupModernUI();
            ConfigureDataGridView();
            LoadVehicleRecords();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(1920, 1080);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.Text = "Truy suất thông tin xe";
            this.ResumeLayout(false);
        }

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
                Text = "Truy suất thông tin xe",
                Font = new Font("Segoe UI", 9),
                ForeColor = textSecondary,
                Location = new Point(20, 58),
                AutoSize = true
            };

            // Sidebar Menu Items
            Button btnGiamSat = CreateSidebarButton("📹  Giám sát", 100, false);
            Button btnPhanTich = CreateSidebarButton("📊  Phân tích giao thông", 160, false);
            Button btnTruyXuat = CreateSidebarButton("🔍  Truy xuất thông tin xe", 220, true);
            Button btnCaiDat = CreateSidebarButton("⚙️  Cài đặt", 280, false);

            btnGiamSat.Click += (s, e) =>
            {
                try
                {
                    CamGiamSat camGiamSat = new CamGiamSat();
                    camGiamSat.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở form giám sát: {ex.Message}");
                }
            };

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
                    MessageBox.Show($"Lỗi khi mở form phân tích: {ex.Message}");
                }
            };

            btnCaiDat.Click += (s, e) =>
            {
                try
                {
                    QuanLyCamera quanLyCamera = new QuanLyCamera();
                    quanLyCamera.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở form cài đặt: {ex.Message}");
                }
            };

            sidebarPanel.Controls.AddRange(new Control[] {
                sidebarTitle, sidebarSubtitle,
                btnGiamSat, btnPhanTich, btnTruyXuat, btnCaiDat
            });

            // Main Content Panel
            Panel mainPanel = new Panel
            {
                Location = new Point(250, 0),
                Size = new Size(this.Width - 250, this.Height),
                BackColor = bgDark,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Header
            Label lblHeader = new Label
            {
                Text = "Truy suất thông tin xe",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(30, 60),
                AutoSize = true
            };
            mainPanel.Controls.Add(lblHeader);

            // Filter Panel
            Panel filterPanel = new Panel
            {
                Location = new Point(30, 120),
                Size = new Size(mainPanel.Width - 60, 250),
                BackColor = cardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            mainPanel.Controls.Add(filterPanel);

            InitializeFilterControls(filterPanel);

            // DataGridView Panel
            Panel dgvPanel = new Panel
            {
                Location = new Point(30, 390),
                Size = new Size(mainPanel.Width - 60, mainPanel.Height - 420),
                BackColor = cardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(20)
            };
            mainPanel.Controls.Add(dgvPanel);

            // Initialize DataGridView
            dgvVehicles = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = cardBg,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(51, 65, 85),
                EnableHeadersVisualStyles = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                MultiSelect = false
            };

            dgvPanel.Controls.Add(dgvVehicles);

            // Add all to form
            this.Controls.AddRange(new Control[] {
                mainPanel,
                sidebarPanel,
                windowControlBar
            });
        }

        private Button CreateSidebarButton(string text, int top, bool active = false)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 11),
                ForeColor = textPrimary,
                BackColor = active ? accentBlue : Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(10, top),
                Size = new Size(230, 50),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 65, 85);

            return button;
        }

        private void InitializeFilterControls(Panel container)
        {
            int xPos = 20;
            int yPos = 20;

            // Time filter section
            Label lblTimeSection = new Label
            {
                Text = "Khoảng thời gian",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(xPos, yPos),
                AutoSize = true
            };
            container.Controls.Add(lblTimeSection);

            cbEnableDateFilter = new CheckBox
            {
                Text = "Lọc theo ngày",
                Location = new Point(xPos, yPos + 30),
                Width = 120,
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10)
            };
            cbEnableDateFilter.CheckedChanged += (s, e) =>
            {
                dtpFromDate.Enabled = cbEnableDateFilter.Checked;
                dtpToDate.Enabled = cbEnableDateFilter.Checked;
            };
            container.Controls.Add(cbEnableDateFilter);

            Label lblFrom = new Label
            {
                Text = "Từ:",
                Location = new Point(xPos, yPos + 65),
                Width = 30,
                ForeColor = textPrimary,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(lblFrom);

            dtpFromDate = new DateTimePicker
            {
                Location = new Point(xPos + 40, yPos + 62),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(dtpFromDate);

            Label lblTo = new Label
            {
                Text = "Đến:",
                Location = new Point(xPos, yPos + 100),
                Width = 35,
                ForeColor = textPrimary,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(lblTo);

            dtpToDate = new DateTimePicker
            {
                Location = new Point(xPos + 40, yPos + 97),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(dtpToDate);

            // Vehicle type filter section
            xPos = 280;
            Label lblTypeSection = new Label
            {
                Text = "Loại phương tiện",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(xPos, yPos),
                AutoSize = true
            };
            container.Controls.Add(lblTypeSection);

            rbTatCa = new RadioButton
            {
                Text = "Tất cả",
                Location = new Point(xPos, yPos + 30),
                Width = 100,
                Checked = true,
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(rbTatCa);

            rbXeMay = new RadioButton
            {
                Text = "Xe máy",
                Location = new Point(xPos + 110, yPos + 30),
                Width = 100,
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(rbXeMay);

            rbXeHoi = new RadioButton
            {
                Text = "Xe Hơi",
                Location = new Point(xPos + 220, yPos + 30),
                Width = 100,
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(rbXeHoi);

            rbXeBuyt = new RadioButton
            {
                Text = "Xe Buýt",
                Location = new Point(xPos, yPos + 60),
                Width = 100,
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(rbXeBuyt);

            rbXeTai = new RadioButton
            {
                Text = "Xe tải",
                Location = new Point(xPos + 110, yPos + 60),
                Width = 100,
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10)
            };
            container.Controls.Add(rbXeTai);

            // Search section
            xPos = 640;
            Label lblSearchSection = new Label
            {
                Text = "Tìm kiếm biển số",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(xPos, yPos),
                AutoSize = true
            };
            container.Controls.Add(lblSearchSection);

            txtSearchPlate = new TextBox
            {
                Location = new Point(xPos, yPos + 30),
                Width = 250,
                Height = 35,
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = textSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Text = "Nhập biển số xe..."
            };

            txtSearchPlate.GotFocus += (s, e) =>
            {
                if (txtSearchPlate.Text == "Nhập biển số xe...")
                {
                    txtSearchPlate.Text = "";
                    txtSearchPlate.ForeColor = textPrimary;
                }
            };

            txtSearchPlate.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearchPlate.Text))
                {
                    txtSearchPlate.Text = "Nhập biển số xe...";
                    txtSearchPlate.ForeColor = textSecondary;
                }
            };
            container.Controls.Add(txtSearchPlate);

            // Action buttons
            btnSearch = new Button
            {
                Text = "🔍 Tìm kiếm",
                Location = new Point(xPos, yPos + 75),
                Size = new Size(120, 40),
                BackColor = accentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Click += BtnSearch_Click;
            container.Controls.Add(btnSearch);

            btnReset = new Button
            {
                Text = "🔄 Đặt lại",
                Location = new Point(xPos + 130, yPos + 75),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(107, 114, 128),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += BtnReset_Click;
            container.Controls.Add(btnReset);
        }

        private void ConfigureDataGridView()
        {
            // Column header style
            dgvVehicles.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 65, 85);
            dgvVehicles.ColumnHeadersDefaultCellStyle.ForeColor = textPrimary;
            dgvVehicles.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            dgvVehicles.ColumnHeadersDefaultCellStyle.Padding = new Padding(10);
            dgvVehicles.ColumnHeadersHeight = 45;

            // Cell style
            dgvVehicles.DefaultCellStyle.BackColor = cardBg;
            dgvVehicles.DefaultCellStyle.ForeColor = textPrimary;
            dgvVehicles.DefaultCellStyle.SelectionBackColor = accentBlue;
            dgvVehicles.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvVehicles.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            dgvVehicles.DefaultCellStyle.Padding = new Padding(10);
            dgvVehicles.RowTemplate.Height = 45;

            // Alternating row style
            dgvVehicles.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(25, 35, 52);

            // Define columns
            dgvVehicles.Columns.Clear();
            dgvVehicles.Columns.Add("record_id", "ID");
            dgvVehicles.Columns.Add("license_plate", "Biển số xe");
            dgvVehicles.Columns.Add("vehicle_type", "Loại xe");
            dgvVehicles.Columns.Add("timestamp", "Thời gian");
            dgvVehicles.Columns.Add("camera_name", "Camera");
            dgvVehicles.Columns.Add("location", "Địa điểm");
            dgvVehicles.Columns.Add("confidence", "Độ tin cậy");

            dgvVehicles.Columns["record_id"].Width = 80;
            dgvVehicles.Columns["license_plate"].Width = 150;
            dgvVehicles.Columns["vehicle_type"].Width = 120;
            dgvVehicles.Columns["timestamp"].Width = 180;
            dgvVehicles.Columns["confidence"].Width = 100;

            // Double-click event for details
            dgvVehicles.CellDoubleClick += DgvVehicles_CellDoubleClick;
        }

        private void BtnSearch_Click(object sender, EventArgs e)
        {
            LoadVehicleRecords();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            cbEnableDateFilter.Checked = false;
            dtpFromDate.Value = DateTime.Now;
            dtpToDate.Value = DateTime.Now;
            rbTatCa.Checked = true;
            txtSearchPlate.Text = "Nhập biển số xe...";
            txtSearchPlate.ForeColor = textSecondary;

            LoadVehicleRecords();
        }

        private void LoadVehicleRecords()
        {
            dgvVehicles.Rows.Clear();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            vr.record_id,
                            vr.license_plate,
                            vr.timestamp,
                            vr.plate_image_path,
                            vr.confidence,
                            v.vehicle_type,
                            c.camera_name,
                            c.location_address
                        FROM Vehicle_Records vr
                        LEFT JOIN Vehicles v ON vr.vehicle_id = v.vehicle_id
                        LEFT JOIN Cameras c ON vr.camera_id = c.camera_id
                        WHERE 1=1";

                    if (cbEnableDateFilter.Checked)
                    {
                        query += " AND CAST(vr.timestamp AS DATE) BETWEEN @fromDate AND @toDate";
                    }

                    string vehicleType = GetSelectedVehicleType();
                    if (!string.IsNullOrEmpty(vehicleType))
                    {
                        query += " AND v.vehicle_type = @vehicleType";
                    }

                    string searchPlate = txtSearchPlate.Text.Trim();
                    if (!string.IsNullOrEmpty(searchPlate) && searchPlate != "Nhập biển số xe...")
                    {
                        query += " AND vr.license_plate LIKE @searchPlate";
                    }

                    query += " ORDER BY vr.timestamp DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (cbEnableDateFilter.Checked)
                        {
                            cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                            cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        }

                        if (!string.IsNullOrEmpty(vehicleType))
                        {
                            cmd.Parameters.AddWithValue("@vehicleType", vehicleType);
                        }

                        if (!string.IsNullOrEmpty(searchPlate) && searchPlate != "Nhập biển số xe...")
                        {
                            cmd.Parameters.AddWithValue("@searchPlate", "" + searchPlate + "");
                        }

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dgvVehicles.Rows.Add(
                                    reader["record_id"],
                                    reader["license_plate"],
                                    GetVehicleTypeName(reader["vehicle_type"]?.ToString() ?? "Other"),
                                    Convert.ToDateTime(reader["timestamp"]).ToString("dd/MM/yyyy HH:mm:ss"),
                                    reader["camera_name"]?.ToString() ?? "N/A",
                                    reader["location_address"]?.ToString() ?? "N/A",
                                    reader["confidence"] != DBNull.Value ?
                                        $"{Convert.ToDecimal(reader["confidence"]):F2}" : "N/A"
                                );

                                // Store full data in Tag
                                dgvVehicles.Rows[dgvVehicles.Rows.Count - 1].Tag = new VehicleRecordData
                                {
                                    RecordId = Convert.ToInt64(reader["record_id"]),
                                    LicensePlate = reader["license_plate"].ToString(),
                                    Timestamp = Convert.ToDateTime(reader["timestamp"]),
                                    ImagePath = reader["plate_image_path"]?.ToString(),
                                    Confidence = reader["confidence"] != DBNull.Value ? Convert.ToDecimal(reader["confidence"]) : 0,
                                    VehicleType = reader["vehicle_type"]?.ToString() ?? "Other",
                                    CameraName = reader["camera_name"]?.ToString(),
                                    Location = reader["location_address"]?.ToString()
                                };
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetSelectedVehicleType()
        {
            if (rbTatCa.Checked) return null;
            if (rbXeMay.Checked) return "Bike";
            if (rbXeHoi.Checked) return "Car";
            if (rbXeBuyt.Checked) return "Bus";
            if (rbXeTai.Checked) return "Truck";
            return null;
        }

        private string GetVehicleTypeName(string type)
        {
            switch (type)
            {
                case "Bike": return "Xe máy";
                case "Car": return "Xe hơi";
                case "Truck": return "Xe tải";
                case "Bus": return "Xe buýt";
                default: return "Khác";
            }
        }

        private void DgvVehicles_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var data = dgvVehicles.Rows[e.RowIndex].Tag as VehicleRecordData;
                if (data != null)
                {
                    VehicleDetailForm detailForm = new VehicleDetailForm(data);
                    detailForm.ShowDialog();
                }
            }
        }
    }

    // Vehicle Record Data Model
    public class VehicleRecordData
    {
        public long RecordId { get; set; }
        public string LicensePlate { get; set; }
        public DateTime Timestamp { get; set; }
        public string ImagePath { get; set; }
        public decimal Confidence { get; set; }
        public string VehicleType { get; set; }
        public string CameraName { get; set; }
        public string Location { get; set; }
    }

    // Vehicle Detail Form (Popup)
    public class VehicleDetailForm : Form
    {
        private VehicleRecordData data;
        private readonly Color bgDark = Color.FromArgb(15, 23, 42);
        private readonly Color cardBg = Color.FromArgb(30, 41, 59);
        private readonly Color textPrimary = Color.FromArgb(248, 250, 252);
        private readonly Color textSecondary = Color.FromArgb(148, 163, 184);
        private readonly Color accentBlue = Color.FromArgb(59, 130, 246);

        public VehicleDetailForm(VehicleRecordData vehicleData)
        {
            this.data = vehicleData;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Size = new Size(800, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Chi tiết phương tiện";
            this.BackColor = bgDark;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Main Panel
            Panel mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgDark,
                Padding = new Padding(30)
            };

            // Title
            Label lblTitle = new Label
            {
                Text = "Thông tin chi tiết phương tiện",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                Location = new Point(30, 20),
                Size = new Size(740, 40),
                ForeColor = textPrimary,
                TextAlign = ContentAlignment.MiddleCenter
            };
            mainPanel.Controls.Add(lblTitle);

            // Image Panel
            Panel imagePanel = new Panel
            {
                Location = new Point(30, 80),
                Size = new Size(740, 300),
                BackColor = cardBg,
                BorderStyle = BorderStyle.FixedSingle
            };
            mainPanel.Controls.Add(imagePanel);

            PictureBox pbImage = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(51, 65, 85),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            if (!string.IsNullOrEmpty(data.ImagePath) && File.Exists(data.ImagePath))
            {
                try
                {
                    pbImage.Image = Image.FromFile(data.ImagePath);
                }
                catch
                {
                    Label lblNoImage = new Label
                    {
                        Text = "Không thể tải ảnh",
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        ForeColor = textSecondary,
                        Font = new Font("Segoe UI", 14)
                    };
                    pbImage.Controls.Add(lblNoImage);
                }
            }
            else
            {
                Label lblNoImage = new Label
                {
                    Text = "Không có ảnh",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = textSecondary,
                    Font = new Font("Segoe UI", 14)
                };
                pbImage.Controls.Add(lblNoImage);
            }

            imagePanel.Controls.Add(pbImage);

            // Information Panel
            Panel infoPanel = new Panel
            {
                Location = new Point(30, 400),
                Size = new Size(740, 200),
                BackColor = cardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(20)
            };
            mainPanel.Controls.Add(infoPanel);

            int yPos = 20;
            AddInfoLabel(infoPanel, "Biển số xe:", data.LicensePlate, yPos);
            yPos += 35;
            AddInfoLabel(infoPanel, "Loại xe:", GetVehicleTypeName(data.VehicleType), yPos);
            yPos += 35;
            AddInfoLabel(infoPanel, "Thời gian:", data.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"), yPos);
            yPos += 35;
            AddInfoLabel(infoPanel, "Camera:", data.CameraName ?? "N/A", yPos);
            yPos += 35;
            AddInfoLabel(infoPanel, "Địa điểm:", data.Location ?? "N/A", yPos);

            // Close button
            Button btnClose = new Button
            {
                Text = "Đóng",
                Location = new Point(350, 620),
                Size = new Size(120, 45),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            mainPanel.Controls.Add(btnClose);

            this.Controls.Add(mainPanel);
        }

        private void AddInfoLabel(Panel panel, string label, string value, int yPos)
        {
            Label lblLabel = new Label
            {
                Text = label,
                Location = new Point(20, yPos),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = textPrimary
            };
            panel.Controls.Add(lblLabel);

            Label lblValue = new Label
            {
                Text = value,
                Location = new Point(230, yPos),
                Size = new Size(480, 25),
                Font = new Font("Segoe UI", 11),
                ForeColor = accentBlue
            };
            panel.Controls.Add(lblValue);
        }

        private string GetVehicleTypeName(string type)
        {
            switch (type)
            {
                case "Bike": return "Xe máy";
                case "Car": return "Xe hơi";
                case "Truck": return "Xe tải";
                case "Bus": return "Xe buýt";
                default: return "Khác";
            }
        }
    }
}