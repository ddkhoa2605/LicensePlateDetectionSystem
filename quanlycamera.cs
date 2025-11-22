using System;
using System.Drawing;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Configuration;

namespace Plate_recor_Final
{
    public class QuanLyCamera : Form
    {
        private readonly Color bgDark = Color.FromArgb(15, 23, 42);
        private readonly Color cardBg = Color.FromArgb(30, 41, 59);
        private readonly Color textPrimary = Color.FromArgb(248, 250, 252);
        private readonly Color textSecondary = Color.FromArgb(148, 163, 184);
        private readonly Color accentBlue = Color.FromArgb(59, 130, 246);
        private readonly Color successGreen = Color.FromArgb(34, 197, 94);
        private readonly Color errorRed = Color.FromArgb(239, 68, 68);

        private TextBox txtTenCamera, txtRTSP, txtDiaChi, txtSearch;
        private ComboBox cboTrangThai;
        private Button btnThem, btnXoa;
        private DataGridView dgvCameras;

        private string connectionString = @"Server=HOANGLAM\LAM;Database=QuanLyGiaoThong;User Id=sa;Password=2211;Encrypt=True;TrustServerCertificate=True;";

        [DllImport("user32.DLL", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();
        [DllImport("user32.DLL", EntryPoint = "SendMessage")]
        private extern static void SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        public QuanLyCamera()
        {
            InitializeComponent();
            SetupModernUI();
            ConfigureDataGridView(); // PHẢI GỌI TRƯỚC KHI LOAD DỮ LIỆU
            InitializeEvents();
            LoadCameras();
        }

        private Button CreateSidebarButton(string text, int top, bool active = false)
        {
            return new Button
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
                FlatAppearance = { BorderSize = 0 }
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(1920, 1080);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.Text = "Quản lý Camera";
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
                Text = "Quản lý Camera",
                Font = new Font("Segoe UI", 9),
                ForeColor = textSecondary,
                Location = new Point(20, 58),
                AutoSize = true
            };

            // Sidebar Menu Items
            Button btnGiamSat = CreateSidebarButton("📹  Giám sát", 100, false);
            Button btnPhanTich = CreateSidebarButton("📊  Phân tích giao thông", 160, false);
            Button btnTruyXuat = CreateSidebarButton("🔍  Truy xuất thông tin xe", 220, false);
            Button btnCaiDat = CreateSidebarButton("⚙️  Cài đặt", 280, true);

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
                    MessageBox.Show($"Lỗi khi mở form giám sát: {ex.Message}");
                }
            };

            btnTruyXuat.Click += (s, e) =>
            {
                try
                {
                    TruySuatThongTinXe truyXuat = new TruySuatThongTinXe();
                    truyXuat.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở form giám sát: {ex.Message}");
                }
            };

            sidebarPanel.Controls.AddRange(new Control[] {
                sidebarTitle, sidebarSubtitle, btnGiamSat, btnPhanTich, btnTruyXuat, btnCaiDat
            });

            // Header title
            Label lblTitle = new Label
            {
                Text = "Cài đặt camera",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(280, 30),
                AutoSize = true
            };

            // Search Panel
            Panel pnlSearch = new Panel
            {
                BackColor = cardBg,
                Location = new Point(270, 80),
                Size = new Size(1600, 60),
                BorderStyle = BorderStyle.None
            };

            txtSearch = new TextBox
            {
                Location = new Point(30, 15),
                Width = 400,
                Font = new Font("Segoe UI", 10),
                BackColor = bgDark,
                ForeColor = textPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Tìm kiếm ..."
            };

            pnlSearch.Controls.Add(txtSearch);

            // Khung thêm camera
            Panel pnlAdd = new Panel
            {
                BackColor = cardBg,
                Location = new Point(270, 160),
                Size = new Size(1600, 220),
                BorderStyle = BorderStyle.None
            };

            Label lblAddTitle = new Label
            {
                Text = "Thêm camera mới",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(30, 20),
                AutoSize = true
            };

            Label lblTen = new Label
            {
                Text = "Tên camera *",
                ForeColor = textSecondary,
                Location = new Point(30, 60),
                AutoSize = true
            };
            txtTenCamera = new TextBox
            {
                Location = new Point(30, 85),
                Width = 400,
                Font = new Font("Segoe UI", 10),
                BackColor = bgDark,
                ForeColor = textPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Nhập Tên Camera"
            };

            Label lblRTSP = new Label
            {
                Text = "Địa chỉ RTSP *",
                ForeColor = textSecondary,
                Location = new Point(460, 60),
                AutoSize = true
            };
            txtRTSP = new TextBox
            {
                Location = new Point(460, 85),
                Width = 400,
                Font = new Font("Segoe UI", 10),
                BackColor = bgDark,
                ForeColor = textPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Camera URL"
            };

            Label lblDiaChi = new Label
            {
                Text = "Địa chỉ *",
                ForeColor = textSecondary,
                Location = new Point(890, 60),
                AutoSize = true
            };
            txtDiaChi = new TextBox
            {
                Location = new Point(890, 85),
                Width = 250,
                Font = new Font("Segoe UI", 10),
                BackColor = bgDark,
                ForeColor = textPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Nhập địa chỉ Camera"
            };

            Label lblTrangThai = new Label
            {
                Text = "Trạng thái *",
                ForeColor = textSecondary,
                Location = new Point(1170, 60),
                AutoSize = true
            };
            cboTrangThai = new ComboBox
            {
                Location = new Point(1170, 85),
                Width = 150,
                Font = new Font("Segoe UI", 10),
                BackColor = bgDark,
                ForeColor = textPrimary,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboTrangThai.Items.AddRange(new string[] { "Active", "Inactive" });

            btnThem = new Button
            {
                Text = "Thêm",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = successGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(1350, 82),
                Size = new Size(100, 35)
            };
            btnThem.FlatAppearance.BorderSize = 0;

            btnXoa = new Button
            {
                Text = "Xóa",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = errorRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(1460, 82),
                Size = new Size(100, 35)
            };
            btnXoa.FlatAppearance.BorderSize = 0;

            pnlAdd.Controls.AddRange(new Control[] {
                lblAddTitle, lblTen, txtTenCamera, lblRTSP, txtRTSP,
                lblDiaChi, txtDiaChi, lblTrangThai, cboTrangThai,
                btnThem, btnXoa
            });

            // Bảng danh sách camera
            Panel pnlList = new Panel
            {
                BackColor = cardBg,
                Location = new Point(270, 400),
                Size = new Size(1600, 600),
                BorderStyle = BorderStyle.None
            };

            Label lblList = new Label
            {
                Text = "Danh sách camera",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(30, 20),
                AutoSize = true
            };

            dgvCameras = new DataGridView
            {
                Location = new Point(30, 60),
                Size = new Size(1530, 500),
                BackgroundColor = cardBg,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                ReadOnly = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            pnlList.Controls.Add(lblList);
            pnlList.Controls.Add(dgvCameras);

            this.Controls.Add(sidebarPanel);
            this.Controls.Add(windowControlBar);
            this.Controls.Add(lblTitle);
            this.Controls.Add(pnlSearch);
            this.Controls.Add(pnlAdd);
            this.Controls.Add(pnlList);
        }

        private void InitializeEvents()
        {
            // Placeholder text events
            txtTenCamera.GotFocus += (s, e) => {
                if (txtTenCamera.Text == "Nhập Tên Camera")
                    txtTenCamera.Text = "";
            };
            txtTenCamera.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtTenCamera.Text))
                    txtTenCamera.Text = "Nhập Tên Camera";
            };

            txtDiaChi.GotFocus += (s, e) => {
                if (txtDiaChi.Text == "Nhập địa chỉ Camera")
                    txtDiaChi.Text = "";
            };
            txtDiaChi.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtDiaChi.Text))
                    txtDiaChi.Text = "Nhập địa chỉ Camera";
            };

            txtRTSP.GotFocus += (s, e) => {
                if (txtRTSP.Text == "Camera URL")
                    txtRTSP.Text = "";
            };
            txtRTSP.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtRTSP.Text))
                    txtRTSP.Text = "Camera URL";
            };

            txtSearch.GotFocus += (s, e) => {
                if (txtSearch.Text == "Tìm kiếm ...")
                    txtSearch.Text = "";
            };
            txtSearch.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                    txtSearch.Text = "Tìm kiếm ...";
            };

            // Button events
            btnThem.Click += btnThem_Click;
            btnXoa.Click += btnXoa_Click;
            txtSearch.TextChanged += txtSearch_TextChanged;
        }

        private void ConfigureDataGridView()
        {
            // Clear existing columns first
            dgvCameras.Columns.Clear();

            // Configure DataGridView appearance
            dgvCameras.ColumnHeadersDefaultCellStyle.BackColor = bgDark;
            dgvCameras.ColumnHeadersDefaultCellStyle.ForeColor = textPrimary;
            dgvCameras.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvCameras.EnableHeadersVisualStyles = false;
            dgvCameras.DefaultCellStyle.BackColor = cardBg;
            dgvCameras.DefaultCellStyle.ForeColor = textPrimary;
            dgvCameras.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            dgvCameras.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 65, 85);
            dgvCameras.DefaultCellStyle.SelectionForeColor = textPrimary;

            dgvCameras.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCameras.MultiSelect = true;
            dgvCameras.AllowUserToAddRows = false;

            // Add columns
            dgvCameras.Columns.Add("camera_name", "Tên Camera");
            dgvCameras.Columns.Add("camera_url", "Địa chỉ RTSP");
            dgvCameras.Columns.Add("location_address", "Địa chỉ");

            // Checkbox column for plate detection
            DataGridViewCheckBoxColumn plateColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Nhận diện biển số",
                Name = "enable_plate_detection",
                Width = 150
            };
            dgvCameras.Columns.Add(plateColumn);

            // ComboBox column for status
            DataGridViewComboBoxColumn statusColumn = new DataGridViewComboBoxColumn
            {
                HeaderText = "Trạng Thái",
                Name = "status",
                Width = 120,
                FlatStyle = FlatStyle.Flat
            };
            statusColumn.Items.AddRange("Active", "Inactive");
            dgvCameras.Columns.Add(statusColumn);

            // Action buttons
            DataGridViewButtonColumn editCol = new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "✏️",
                UseColumnTextForButtonValue = true,
                Width = 60
            };
            DataGridViewButtonColumn delCol = new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "🗑️",
                UseColumnTextForButtonValue = true,
                Width = 60
            };

            dgvCameras.Columns.Add(editCol);
            dgvCameras.Columns.Add(delCol);

            // Set column readonly properties
            foreach (DataGridViewColumn col in dgvCameras.Columns)
            {
                if (col.Name != "enable_plate_detection" && col.Name != "status" &&
                    !(col is DataGridViewButtonColumn))
                {
                    col.ReadOnly = true;
                }
            }

            // Handle button clicks
            dgvCameras.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < dgvCameras.Rows.Count)
                {
                    if (e.ColumnIndex == dgvCameras.Columns.Count - 2) // Edit button
                    {
                        EditCamera(e.RowIndex);
                    }
                    else if (e.ColumnIndex == dgvCameras.Columns.Count - 1) // Delete button
                    {
                        DeleteSingleCamera(e.RowIndex);
                    }
                }
            };

            // DataGridView events for real-time updates
            dgvCameras.CellValueChanged += dataCameraURL_CellValueChanged;
            dgvCameras.CurrentCellDirtyStateChanged += dataCameraURL_CurrentCellDirtyStateChanged;
        }

        private int ExecuteNonQuery(string query, SqlParameter[] parameters = null)
        {
            int result = 0;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (parameters != null)
                        {
                            cmd.Parameters.AddRange(parameters);
                        }

                        conn.Open();
                        result = cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi thực thi câu lệnh: " + ex.Message);
            }
            return result;
        }

        private DataTable GetData(string query, SqlParameter[] parameters = null)
        {
            DataTable dt = new DataTable();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (parameters != null)
                        {
                            cmd.Parameters.AddRange(parameters);
                        }

                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            dt.Load(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi lấy dữ liệu: " + ex.Message);
            }
            return dt;
        }

        private void LoadCameras(string keyword = "")
        {
            try
            {
                string query = @"SELECT camera_id, camera_name, camera_url, 
                               location_address, status, 
                               ISNULL(enable_plate_detection, 0) as enable_plate_detection 
                               FROM Cameras WHERE 1=1";

                SqlParameter[] parameters = null;

                if (!string.IsNullOrEmpty(keyword) && keyword != "Tìm kiếm ...")
                {
                    query += " AND (camera_name LIKE @kw OR location_address LIKE @kw)";
                    parameters = new SqlParameter[]
                    {
                        new SqlParameter("@kw", "%" + keyword + "%")
                    };
                }

                query += " ORDER BY camera_id DESC";

                DataTable dt = GetData(query, parameters);

                dgvCameras.Rows.Clear();

                foreach (DataRow row in dt.Rows)
                {
                    int rowIndex = dgvCameras.Rows.Add(
                        row["camera_name"],
                        row["camera_url"],
                        row["location_address"],
                        Convert.ToBoolean(row["enable_plate_detection"]),
                        row["status"]
                    );

                    dgvCameras.Rows[rowIndex].Tag = row["camera_id"];
                }

                // Update row count display
                UpdateRowCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải danh sách camera: " + ex.Message,
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateRowCount()
        {
            // You can add a label to show row count if needed
            // lblRowCount.Text = $"Tổng số: {dgvCameras.Rows.Count} camera";
        }

        private void btnThem_Click(object sender, EventArgs e)
        {
            try
            {
                string tenCamera = txtTenCamera.Text.Trim();
                string cameraUrl = txtRTSP.Text.Trim();
                string diaChiCamera = txtDiaChi.Text.Trim();
                string trangThai = cboTrangThai.Text;

                // Validate input
                if (tenCamera == "" || tenCamera == "Nhập Tên Camera")
                {
                    ShowMessage("Vui lòng nhập tên camera!", "Cảnh báo", MessageBoxIcon.Warning);
                    txtTenCamera.Focus();
                    return;
                }

                if (cameraUrl == "" || cameraUrl == "Camera URL")
                {
                    ShowMessage("Vui lòng nhập URL camera!", "Cảnh báo", MessageBoxIcon.Warning);
                    txtRTSP.Focus();
                    return;
                }

                if (diaChiCamera == "" || diaChiCamera == "Nhập địa chỉ Camera")
                {
                    ShowMessage("Vui lòng nhập địa chỉ camera!", "Cảnh báo", MessageBoxIcon.Warning);
                    txtDiaChi.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(trangThai))
                {
                    ShowMessage("Vui lòng chọn trạng thái!", "Cảnh báo", MessageBoxIcon.Warning);
                    cboTrangThai.Focus();
                    return;
                }

                // Check if we're in update mode
                if (btnThem.Tag != null)
                {
                    // Update existing camera
                    int cameraId = (int)btnThem.Tag;
                    string query = @"UPDATE Cameras 
                                   SET camera_name = @name, 
                                       camera_url = @url, 
                                       location_address = @location, 
                                       status = @status 
                                   WHERE camera_id = @id";

                    SqlParameter[] parameters = new SqlParameter[]
                    {
                        new SqlParameter("@name", tenCamera),
                        new SqlParameter("@url", cameraUrl),
                        new SqlParameter("@location", diaChiCamera),
                        new SqlParameter("@status", trangThai),
                        new SqlParameter("@id", cameraId)
                    };

                    int result = ExecuteNonQuery(query, parameters);

                    if (result > 0)
                    {
                        ShowMessage("Cập nhật camera thành công!", "Thành công", MessageBoxIcon.Information);
                        ClearForm();
                        LoadCameras();
                    }
                }
                else
                {
                    // Add new camera
                    string query = @"INSERT INTO Cameras (camera_name, camera_url, location_address, status, enable_plate_detection) 
                                   VALUES (@name, @url, @location, @status, 0)";

                    SqlParameter[] parameters = new SqlParameter[]
                    {
                        new SqlParameter("@name", tenCamera),
                        new SqlParameter("@url", cameraUrl),
                        new SqlParameter("@location", diaChiCamera),
                        new SqlParameter("@status", trangThai)
                    };

                    int result = ExecuteNonQuery(query, parameters);

                    if (result > 0)
                    {
                        ShowMessage("Thêm camera thành công!", "Thành công", MessageBoxIcon.Information);
                        ClearForm();
                        LoadCameras();
                    }
                    else
                    {
                        ShowMessage("Thêm camera thất bại!", "Lỗi", MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Lỗi khi thêm camera: " + ex.Message, "Lỗi", MessageBoxIcon.Error);
            }
        }

        private void btnXoa_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvCameras.SelectedRows.Count == 0)
                {
                    ShowMessage("Vui lòng chọn camera cần xóa!", "Cảnh báo", MessageBoxIcon.Warning);
                    return;
                }

                DialogResult result = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa {dgvCameras.SelectedRows.Count} camera đã chọn?\n\nLƯU Ý: Tất cả dữ liệu nhận diện liên quan cũng sẽ bị xóa.",
                    "Xác nhận xóa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    int successCount = 0;
                    int failCount = 0;

                    foreach (DataGridViewRow row in dgvCameras.SelectedRows)
                    {
                        if (row.Tag != null)
                        {
                            int cameraId = Convert.ToInt32(row.Tag);

                            // Xóa theo thứ tự: Vehicle_Records trước, sau đó đến Cameras
                            bool deleteSuccess = DeleteCameraWithRecords(cameraId);

                            if (deleteSuccess)
                                successCount++;
                            else
                                failCount++;
                        }
                    }

                    string message = $"Xóa thành công: {successCount} camera";
                    if (failCount > 0)
                        message += $"\nXóa thất bại: {failCount} camera";

                    MessageBox.Show(message, "Kết quả",
                        MessageBoxButtons.OK,
                        failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                    LoadCameras();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Lỗi khi xóa camera: " + ex.Message, "Lỗi", MessageBoxIcon.Error);
            }
        }

        private bool DeleteCameraWithRecords(int cameraId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Bắt đầu transaction để đảm bảo tính toàn vẹn dữ liệu
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Cập nhật vehicle_id thành NULL trong Vehicle_Records trước
                            string updateQuery = @"
                        UPDATE Vehicle_Records 
                        SET vehicle_id = NULL 
                        WHERE camera_id = @cameraId";

                            using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@cameraId", cameraId);
                                updateCmd.ExecuteNonQuery();
                            }

                            // 2. Xóa các bản ghi trong Vehicle_Records
                            string deleteRecordsQuery = "DELETE FROM Vehicle_Records WHERE camera_id = @cameraId";
                            using (SqlCommand deleteRecordsCmd = new SqlCommand(deleteRecordsQuery, conn, transaction))
                            {
                                deleteRecordsCmd.Parameters.AddWithValue("@cameraId", cameraId);
                                deleteRecordsCmd.ExecuteNonQuery();
                            }

                            // 3. Xóa camera
                            string deleteCameraQuery = "DELETE FROM Cameras WHERE camera_id = @cameraId";
                            using (SqlCommand deleteCameraCmd = new SqlCommand(deleteCameraQuery, conn, transaction))
                            {
                                deleteCameraCmd.Parameters.AddWithValue("@cameraId", cameraId);
                                int result = deleteCameraCmd.ExecuteNonQuery();

                                if (result > 0)
                                {
                                    transaction.Commit();
                                    return true;
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi xóa camera ID {cameraId}: {ex.Message}");
            }
        }

        private void DeleteSingleCamera(int rowIndex)
        {
            try
            {
                if (rowIndex >= 0 && rowIndex < dgvCameras.Rows.Count && dgvCameras.Rows[rowIndex].Tag != null)
                {
                    int cameraId = Convert.ToInt32(dgvCameras.Rows[rowIndex].Tag);
                    string cameraName = dgvCameras.Rows[rowIndex].Cells["camera_name"].Value?.ToString() ?? "Unknown";

                    DialogResult result = MessageBox.Show(
                        $"Bạn có chắc chắn muốn xóa camera '{cameraName}'?",
                        "Xác nhận xóa",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        string query = "DELETE FROM Cameras WHERE camera_id = @id";
                        SqlParameter[] parameters = new SqlParameter[]
                        {
                            new SqlParameter("@id", cameraId)
                        };

                        int deleteResult = ExecuteNonQuery(query, parameters);

                        if (deleteResult > 0)
                        {
                            ShowMessage("Xóa camera thành công!", "Thành công", MessageBoxIcon.Information);
                            LoadCameras();
                        }
                        else
                        {
                            ShowMessage("Xóa camera thất bại!", "Lỗi", MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Lỗi khi xóa camera: " + ex.Message, "Lỗi", MessageBoxIcon.Error);
            }
        }

        private void EditCamera(int rowIndex)
        {
            try
            {
                if (rowIndex >= 0 && rowIndex < dgvCameras.Rows.Count && dgvCameras.Rows[rowIndex].Tag != null)
                {
                    // Fill form with selected camera data for editing
                    DataGridViewRow row = dgvCameras.Rows[rowIndex];

                    txtTenCamera.Text = row.Cells["camera_name"].Value?.ToString() ?? "";
                    txtRTSP.Text = row.Cells["camera_url"].Value?.ToString() ?? "";
                    txtDiaChi.Text = row.Cells["location_address"].Value?.ToString() ?? "";
                    cboTrangThai.Text = row.Cells["status"].Value?.ToString() ?? "";

                    // Store the camera_id for update
                    btnThem.Tag = row.Tag;
                    btnThem.Text = "Cập nhật";

                    // Change button color to indicate update mode
                    btnThem.BackColor = accentBlue;
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Lỗi khi chỉnh sửa camera: " + ex.Message, "Lỗi", MessageBoxIcon.Error);
            }
        }

        private void dataCameraURL_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < dgvCameras.Rows.Count && e.RowIndex != dgvCameras.NewRowIndex)
            {
                try
                {
                    DataGridViewRow row = dgvCameras.Rows[e.RowIndex];

                    if (row.Tag != null)
                    {
                        int cameraId = Convert.ToInt32(row.Tag);

                        // Update plate detection
                        if (e.ColumnIndex == dgvCameras.Columns["enable_plate_detection"].Index)
                        {
                            bool enablePlateDetection = Convert.ToBoolean(row.Cells["enable_plate_detection"].Value);

                            string query = "UPDATE Cameras SET enable_plate_detection = @enable WHERE camera_id = @id";
                            SqlParameter[] parameters = new SqlParameter[]
                            {
                                new SqlParameter("@enable", enablePlateDetection),
                                new SqlParameter("@id", cameraId)
                            };

                            ExecuteNonQuery(query, parameters);
                        }
                        // Update status
                        else if (e.ColumnIndex == dgvCameras.Columns["status"].Index)
                        {
                            string newStatus = row.Cells["status"].Value?.ToString();

                            if (!string.IsNullOrEmpty(newStatus))
                            {
                                string query = "UPDATE Cameras SET status = @status WHERE camera_id = @id";
                                SqlParameter[] parameters = new SqlParameter[]
                                {
                                    new SqlParameter("@status", newStatus),
                                    new SqlParameter("@id", cameraId)
                                };

                                ExecuteNonQuery(query, parameters);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage("Lỗi khi cập nhật: " + ex.Message, "Lỗi", MessageBoxIcon.Error);
                    LoadCameras(); // Reload to restore correct state
                }
            }
        }

        private void dataCameraURL_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvCameras.IsCurrentCellDirty)
            {
                if (dgvCameras.CurrentCell is DataGridViewCheckBoxCell ||
                    dgvCameras.CurrentCell is DataGridViewComboBoxCell)
                {
                    dgvCameras.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            LoadCameras(txtSearch.Text.Trim());
        }

        private void ClearForm()
        {
            txtTenCamera.Text = "Nhập Tên Camera";
            txtDiaChi.Text = "Nhập địa chỉ Camera";
            txtRTSP.Text = "Camera URL";
            cboTrangThai.SelectedIndex = -1;

            // Reset button to "Thêm" mode
            btnThem.Text = "Thêm";
            btnThem.BackColor = successGreen;
            btnThem.Tag = null;
        }

        private void ShowMessage(string message, string title, MessageBoxIcon icon)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
        }
    }
}