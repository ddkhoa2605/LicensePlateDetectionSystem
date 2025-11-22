using Plate_recor_Final;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Plate_recor_Final
{
    public partial class PhanTichGiaoThong : Form
    {
        private string connectionString = "Server=.;Database=QuanLyGiaoThong;Integrated Security=true;";

        private readonly Color bgDark = Color.FromArgb(15, 23, 42);
        private readonly Color cardBg = Color.FromArgb(30, 41, 59);
        private readonly Color accentBlue = Color.FromArgb(59, 130, 246);
        private readonly Color textPrimary = Color.FromArgb(248, 250, 252);
        private readonly Color textSecondary = Color.FromArgb(148, 163, 184);

        private DateTimePicker dtpFromDate;
        private DateTimePicker dtpToDate;
        private ComboBox cboLocation;
        private Button btnFilter;
        private Button btnExport;
        private Chart chartDensity;
        private Chart chartVehicleTypes;
        private FlowLayoutPanel flowCards;

        [DllImport("user32.DLL", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();
        [DllImport("user32.DLL", EntryPoint = "SendMessage")]
        private extern static void SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        public PhanTichGiaoThong()
        {
            InitializeComponent();
            SetupModernUI();
            LoadLocations();
            LoadTrafficData();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(1920, 1080);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.Text = "Phân tích giao thông";
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
                Text = "Phân tích giao thông",
                Font = new Font("Segoe UI", 9),
                ForeColor = textSecondary,
                Location = new Point(20, 58),
                AutoSize = true
            };

            // Sidebar Menu Items
            Button btnGiamSat = CreateSidebarButton("📹  Giám sát", 100, false);
            Button btnPhanTich = CreateSidebarButton("📊  Phân tích giao thông", 160, true);
            Button btnTruyXuat = CreateSidebarButton("🔍  Truy xuất thông tin xe", 220, false);
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
                    MessageBox.Show($"Lỗi khi mở form truy xuất: {ex.Message}");
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

            // === Header ===
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = cardBg,
                Padding = new Padding(30, 0, 30, 0)
            };
            Label lblTitle = new Label
            {
                Text = "Phân tích giao thông",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(30, 25),
                AutoSize = true
            };
            header.Controls.Add(lblTitle);

            // === Main panel ===
            Panel mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgDark,
                AutoScroll = true,
                Padding = new Padding(30, 20, 30, 20)
            };

            // --- Bộ lọc ---
            Panel pnlFilter = new Panel
            {
                Location = new Point(30, 20),
                Size = new Size(1300, 70),
                BackColor = cardBg
            };

            // Dùng FlowLayoutPanel để canh lề tự động
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Padding = new Padding(15, 15, 15, 15),
                Margin = new Padding(10),
                BackColor = cardBg
            };

            // Các control trong hàng lọc
            Label lblFrom = new Label
            {
                Text = "Từ ngày:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = textPrimary,
                AutoSize = true,
                Margin = new Padding(5, 8, 5, 0)
            };
            dtpFromDate = new DateTimePicker
            {
                Width = 130,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddDays(-7),
                Margin = new Padding(5, 3, 15, 3)
            };

            Label lblTo = new Label
            {
                Text = "Đến ngày:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = textPrimary,
                AutoSize = true,
                Margin = new Padding(5, 8, 5, 0)
            };
            dtpToDate = new DateTimePicker
            {
                Width = 130,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now,
                Margin = new Padding(5, 3, 15, 3)
            };

            Label lblLocation = new Label
            {
                Text = "Khu vực:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = textPrimary,
                AutoSize = true,
                Margin = new Padding(5, 8, 5, 0)
            };
            cboLocation = new ComboBox
            {
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(5, 3, 15, 3)
            };

            btnFilter = new Button
            { 
                Text = "Lọc dữ liệu",
                Size = new Size(130, 35),
                BackColor = accentBlue,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(15, 3, 3, 3)
            };
            btnFilter.FlatAppearance.BorderSize = 0;
            btnFilter.Click += BtnFilter_Click;

            btnExport = new Button
            {
                Text = "Xuất báo cáo",
                Size = new Size(130, 35),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(15, 3, 3, 3)
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += BtnExport_Click;

            // Thêm vào flow panel
            flow.Controls.AddRange(new Control[]
            {
                lblFrom, dtpFromDate,
                lblTo, dtpToDate,
                lblLocation, cboLocation,
                btnFilter, btnExport
            });

            // Gắn vào panel chính
            pnlFilter.Controls.Add(flow);
            mainPanel.Controls.Add(pnlFilter);

            // --- Biểu đồ ---
            Panel pnlCharts = new Panel { Location = new Point(30, 100), Size = new Size(1300, 450) };

            Panel densityPanel = new Panel { Location = new Point(0, 0), Size = new Size(640, 450), BackColor = cardBg, Padding = new Padding(10) };
            chartDensity = new Chart { Dock = DockStyle.Fill, BackColor = cardBg };
            InitializeDensityChart();
            densityPanel.Controls.Add(chartDensity);

            Panel vehiclePanel = new Panel { Location = new Point(660, 0), Size = new Size(640, 450), BackColor = cardBg, Padding = new Padding(10) };
            chartVehicleTypes = new Chart { Dock = DockStyle.Fill, BackColor = cardBg };
            InitializeVehicleTypesChart();
            vehiclePanel.Controls.Add(chartVehicleTypes);

            pnlCharts.Controls.AddRange(new Control[] { densityPanel, vehiclePanel });
            mainPanel.Controls.Add(pnlCharts);

            // --- Bảng điều khiển xu hướng ---
            Panel pnlTrends = CreateTrendsDashboard();
            mainPanel.Controls.Add(pnlTrends);

            // === Add all ===
            this.Controls.Add(mainPanel);
            this.Controls.Add(header);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(windowControlBar);
        }

        private Panel CreateTrendsDashboard()
        {
            Panel pnlTrends = new Panel
            {
                Location = new Point(30, 570),
                Size = new Size(1300, 300),
                BackColor = cardBg,
                Padding = new Padding(20)
            };

            Label lblTitle = new Label
            {
                Text = "Phân tích xu hướng giao thông",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(20, 10),
                AutoSize = true
            };

            // Tạo layout dạng card cho các chỉ số
            flowCards = new FlowLayoutPanel
            {
                Location = new Point(20, 50),
                Size = new Size(1260, 200),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true
            };

            // Các card chỉ số
            CreateMetricCard(flowCards, "Tổng phương tiện", "0", "Tổng số xe trong khoảng thời gian");
            CreateMetricCard(flowCards, "Giờ cao điểm", "--:--", "Giờ có lưu lượng cao nhất");
            CreateMetricCard(flowCards, "Tăng trưởng", "0%", "So với kỳ trước");
            CreateMetricCard(flowCards, "Tỷ lệ xe máy", "0%", "Chiếm tỷ trọng lớn nhất");
            CreateMetricCard(flowCards, "Trung bình/giờ", "0", "Số xe trung bình mỗi giờ");
            CreateMetricCard(flowCards, "Ngày bận nhất", "--", "Ngày có lưu lượng cao nhất");

            pnlTrends.Controls.AddRange(new Control[] { lblTitle, flowCards });
            return pnlTrends;
        }

        private void CreateMetricCard(FlowLayoutPanel parent, string title, string value, string description)
        {
            Panel card = new Panel
            {
                Size = new Size(300, 80),
                BackColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(10, 5, 10, 5),
                Padding = new Padding(15)
            };

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9),
                ForeColor = textSecondary,
                Location = new Point(0, 0),
                AutoSize = true
            };

            Label lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = accentBlue,
                Location = new Point(0, 20),
                AutoSize = true,
                Name = "lblValue"
            };

            Label lblDesc = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 8),
                ForeColor = textSecondary,
                Location = new Point(0, 55),
                AutoSize = true
            };

            card.Controls.AddRange(new Control[] { lblTitle, lblValue, lblDesc });
            parent.Controls.Add(card);
        }

        // ===== Database & Logic =====

        private void LoadLocations()
        {
            cboLocation.Items.Clear();
            cboLocation.Items.Add("Tất cả khu vực");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(
                        "SELECT DISTINCT location_address FROM Cameras WHERE location_address IS NOT NULL ORDER BY location_address",
                        conn
                    );

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        cboLocation.Items.Add(reader["location_address"].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải khu vực: " + ex.Message);
            }

            cboLocation.SelectedIndex = 0;
        }

        private void BtnFilter_Click(object sender, EventArgs e)
        {
            if (dtpFromDate.Value > dtpToDate.Value)
            {
                MessageBox.Show("Ngày bắt đầu phải nhỏ hơn ngày kết thúc!");
                return;
            }

            LoadTrafficData();
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Xuất báo cáo",
                    FileName = $"BaoCaoGiaoThong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // TODO: Implement export to Excel functionality
                    MessageBox.Show("Tính năng xuất Excel đang được phát triển!", "Thông báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất báo cáo: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadTrafficData()
        {
            try
            {
                LoadDensityData();
                LoadVehicleTypesData();
                LoadTrendsAnalysis();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải dữ liệu: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDensityData()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string locationFilter = cboLocation.SelectedIndex > 0 ? " AND c.location_address = @location" : "";

                    string queryDensity = @"
                SELECT DATEPART(HOUR, vr.timestamp) AS Hour, COUNT(*) AS VehicleCount
                FROM Vehicle_Records vr
                INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                WHERE vr.timestamp BETWEEN @fromDate AND @toDate " + locationFilter + @"
                GROUP BY DATEPART(HOUR, vr.timestamp)
                ORDER BY Hour";

                    chartDensity.Series[0].Points.Clear();

                    using (SqlCommand cmd = new SqlCommand(queryDensity, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int hour = (int)reader["Hour"];
                                int count = (int)reader["VehicleCount"];
                                chartDensity.Series[0].Points.AddXY($"{hour:00}:00", count);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải dữ liệu mật độ: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadVehicleTypesData()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string locationFilter = cboLocation.SelectedIndex > 0 ? " AND c.location_address = @location" : "";

                    string query = @"
                SELECT 
                    CASE 
                        WHEN v.vehicle_type IN ('bike','motorcycle') THEN 'Xe máy'
                        WHEN v.vehicle_type = 'car' THEN 'Ô tô'
                        WHEN v.vehicle_type = 'truck' THEN 'Xe tải'
                        WHEN v.vehicle_type = 'bus' THEN 'Xe buýt'
                        ELSE 'Khác'
                    END AS VehicleType,
                    COUNT(*) AS Count
                FROM Vehicle_Records vr
                INNER JOIN Vehicles v ON vr.vehicle_id = v.vehicle_id
                INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                WHERE vr.timestamp BETWEEN @fromDate AND @toDate " + locationFilter + @"
                GROUP BY 
                    CASE 
                        WHEN v.vehicle_type IN ('bike','motorcycle') THEN 'Xe máy'
                        WHEN v.vehicle_type = 'car' THEN 'Ô tô'
                        WHEN v.vehicle_type = 'truck' THEN 'Xe tải'
                        WHEN v.vehicle_type = 'bus' THEN 'Xe buýt'
                        ELSE 'Khác'
                    END";

                    chartVehicleTypes.Series[0].Points.Clear();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string vehicleType = reader["VehicleType"].ToString();
                                int count = (int)reader["Count"];
                                chartVehicleTypes.Series[0].Points.AddXY(vehicleType, count);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải dữ liệu loại xe: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadTrendsAnalysis()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string locationFilter = cboLocation.SelectedIndex > 0 ? " AND c.location_address = @location" : "";

                    // Lấy tổng số xe hiện tại
                    int currentTotal = 0;
                    string totalQuery = @"
                        SELECT COUNT(*) as TotalVehicles
                        FROM Vehicle_Records vr
                        INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                        WHERE vr.timestamp BETWEEN @fromDate AND @toDate " + locationFilter;

                    using (SqlCommand cmd = new SqlCommand(totalQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        currentTotal = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Cập nhật card tổng phương tiện
                    UpdateMetricCard(0, currentTotal.ToString("N0"));

                    // Lấy giờ cao điểm
                    string peakHourQuery = @"
                        SELECT TOP 1 DATEPART(HOUR, timestamp) as PeakHour, COUNT(*) as Count
                        FROM Vehicle_Records vr
                        INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                        WHERE vr.timestamp BETWEEN @fromDate AND @toDate " + locationFilter + @"
                        GROUP BY DATEPART(HOUR, timestamp)
                        ORDER BY COUNT(*) DESC";

                    using (SqlCommand cmd = new SqlCommand(peakHourQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int peakHour = (int)reader["PeakHour"];
                                UpdateMetricCard(1, $"{peakHour:00}:00");
                            }
                            else
                            {
                                UpdateMetricCard(1, "N/A");
                            }
                        }
                    }

                    // Tính tăng trưởng so với kỳ trước
                    int previousTotal = 0;
                    string previousQuery = @"
                        SELECT COUNT(*) as PreviousTotal
                        FROM Vehicle_Records vr
                        INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                        WHERE vr.timestamp BETWEEN DATEADD(DAY, -7, @fromDate) AND DATEADD(DAY, -7, @toDate) " + locationFilter;

                    using (SqlCommand cmd = new SqlCommand(previousQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        object result = cmd.ExecuteScalar();
                        previousTotal = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }

                    double growth = 0;
                    if (previousTotal > 0)
                    {
                        growth = (currentTotal - previousTotal) * 100.0 / previousTotal;
                    }
                    UpdateMetricCard(2, $"{growth:+#;-#;0}%");

                    // Loại xe phổ biến nhất
                    string topVehicleQuery = @"
                        SELECT TOP 1 
                            CASE 
                                WHEN v.vehicle_type IN ('bike','motorcycle') THEN 'Xe máy'
                                WHEN v.vehicle_type = 'car' THEN 'Ô tô'
                                WHEN v.vehicle_type = 'truck' THEN 'Xe tải'
                                WHEN v.vehicle_type = 'bus' THEN 'Xe buýt'
                                ELSE 'Khác'
                            END as VehicleType,
                            CAST(COUNT(*) * 100.0 / @total as decimal(5,2)) as Percentage
                        FROM Vehicle_Records vr
                        INNER JOIN Vehicles v ON vr.vehicle_id = v.vehicle_id
                        INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                        WHERE vr.timestamp BETWEEN @fromDate AND @toDate " + locationFilter + @"
                        GROUP BY 
                            CASE 
                                WHEN v.vehicle_type IN ('bike','motorcycle') THEN 'Xe máy'
                                WHEN v.vehicle_type = 'car' THEN 'Ô tô'
                                WHEN v.vehicle_type = 'truck' THEN 'Xe tải'
                                WHEN v.vehicle_type = 'bus' THEN 'Xe buýt'
                                ELSE 'Khác'
                            END
                        ORDER BY COUNT(*) DESC";

                    using (SqlCommand cmd = new SqlCommand(topVehicleQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        cmd.Parameters.AddWithValue("@total", currentTotal);
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                double percentage = Convert.ToDouble(reader["Percentage"]);
                                UpdateMetricCard(3, $"{percentage:F1}%");
                            }
                            else
                            {
                                UpdateMetricCard(3, "0%");
                            }
                        }
                    }

                    // Trung bình xe mỗi giờ
                    double hoursDiff = (dtpToDate.Value - dtpFromDate.Value).TotalHours;
                    double avgPerHour = hoursDiff > 0 ? currentTotal / hoursDiff : 0;
                    UpdateMetricCard(4, avgPerHour.ToString("F1"));

                    // Ngày bận nhất
                    string busiestDayQuery = @"
                        SELECT TOP 1 CONVERT(VARCHAR, vr.timestamp, 103) as BusiestDate, COUNT(*) as Count
                        FROM Vehicle_Records vr
                        INNER JOIN Cameras c ON vr.camera_id = c.camera_id
                        WHERE vr.timestamp BETWEEN @fromDate AND @toDate " + locationFilter + @"
                        GROUP BY CONVERT(VARCHAR, vr.timestamp, 103)
                        ORDER BY COUNT(*) DESC";

                    using (SqlCommand cmd = new SqlCommand(busiestDayQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", dtpFromDate.Value.Date);
                        cmd.Parameters.AddWithValue("@toDate", dtpToDate.Value.Date.AddDays(1).AddSeconds(-1));
                        if (cboLocation.SelectedIndex > 0)
                            cmd.Parameters.AddWithValue("@location", cboLocation.SelectedItem.ToString());

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string busiestDate = reader["BusiestDate"].ToString();
                                UpdateMetricCard(5, busiestDate);
                            }
                            else
                            {
                                UpdateMetricCard(5, "N/A");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải phân tích xu hướng: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateMetricCard(int cardIndex, string value)
        {
            if (flowCards.Controls.Count > cardIndex)
            {
                Panel card = (Panel)flowCards.Controls[cardIndex];
                foreach (Control control in card.Controls)
                {
                    if (control is Label label && label.Name == "lblValue")
                    {
                        label.Text = value;
                        break;
                    }
                }
            }
        }

        // ===== UI Helper =====
        private void InitializeDensityChart()
        {
            chartDensity.ChartAreas.Clear();
            ChartArea area = new ChartArea("Density");
            area.AxisX.Title = "Giờ";
            area.AxisY.Title = "Số lượng xe";
            area.BackColor = cardBg;
            area.AxisX.LabelStyle.ForeColor = textSecondary;
            area.AxisY.LabelStyle.ForeColor = textSecondary;
            area.AxisX.TitleForeColor = textSecondary;
            area.AxisY.TitleForeColor = textSecondary;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(51, 65, 85);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(51, 65, 85);
            chartDensity.ChartAreas.Add(area);

            Series s = new Series("Mật độ")
            {
                ChartType = SeriesChartType.Line,
                Color = accentBlue,
                BorderWidth = 3,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 8,
                MarkerColor = accentBlue
            };
            chartDensity.Series.Add(s);

            Title title = new Title("Biểu đồ mật độ giao thông", Docking.Top, new Font("Segoe UI", 14, FontStyle.Bold), textPrimary);
            chartDensity.Titles.Add(title);

            // Add legend
            Legend legend = new Legend
            {
                Docking = Docking.Bottom,
                ForeColor = textSecondary,
                Font = new Font("Segoe UI", 9)
            };
            chartDensity.Legends.Add(legend);
        }

        private void InitializeVehicleTypesChart()
        {
            chartVehicleTypes.ChartAreas.Clear();
            ChartArea area = new ChartArea("VehicleTypes");
            area.BackColor = cardBg;
            chartVehicleTypes.ChartAreas.Add(area);

            Series s = new Series("Loại xe")
            {
                ChartType = SeriesChartType.Pie,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                LabelForeColor = textPrimary,
                CustomProperties = "PieLabelStyle=Outside"
            };
            chartVehicleTypes.Series.Add(s);

            Title title = new Title("Tỷ lệ các loại phương tiện", Docking.Top, new Font("Segoe UI", 14, FontStyle.Bold), textPrimary);
            chartVehicleTypes.Titles.Add(title);

            // Add legend
            Legend legend = new Legend
            {
                Docking = Docking.Right,
                ForeColor = textSecondary,
                Font = new Font("Segoe UI", 9)
            };
            chartVehicleTypes.Legends.Add(legend);
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
    }
}