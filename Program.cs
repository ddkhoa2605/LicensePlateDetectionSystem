using System;
using System.Windows.Forms;

namespace Plate_recor_Final
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ← THÊM: Xử lý sự kiện thoát ứng dụng
            Application.ApplicationExit += OnApplicationExit;

            // Chạy form chính
            Application.Run(new CamGiamSat());
        }

        /// <summary>
        /// ← THÊM MỚI: Xử lý khi ứng dụng thoát - cleanup detection service
        /// </summary>
        private static void OnApplicationExit(object sender, EventArgs e)
        {
            try
            {
                var service = PlateDetectionService.Instance;
                var runningCameras = service.GetRunningCameras();

                if (runningCameras.Count > 0)
                {
                    Console.WriteLine($"[APP EXIT] Stopping {runningCameras.Count} running detection(s)...");
                    service.StopAllDetections();
                    Console.WriteLine("[APP EXIT] All detections stopped.");
                }
                else
                {
                    Console.WriteLine("[APP EXIT] No running detections.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APP EXIT ERROR] {ex.Message}");
            }
        }
    }
}