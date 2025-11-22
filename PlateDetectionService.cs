using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Plate_recor_Final
{
    public class PlateDetectionService
    {
        private static PlateDetectionService _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<int, ProcessInfo> _runningProcesses = new Dictionary<int, ProcessInfo>();

        // Đường dẫn cấu hình
        private readonly string _pythonExePath;
        private readonly string _detectorScriptPath;
        private readonly string _modelPath;
        private readonly string _ocrModelPath;      
        private readonly string _ocrConfigPath;     
        private readonly string _outputDir;
        private readonly string _logDir;

        private class ProcessInfo
        {
            public Process Process { get; set; }
            public int CameraId { get; set; }
            public string RtspUrl { get; set; }
            public DateTime StartTime { get; set; }
            public StringBuilder OutputLog { get; set; }
            public StringBuilder ErrorLog { get; set; }
        }

        private PlateDetectionService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            _pythonExePath = "python";
            _detectorScriptPath = Path.Combine(baseDir, "scripts", "plate_detector.py");
            _modelPath = Path.Combine(baseDir, "models", "yolov11_plate.pt");

            // ← THÊM MỚI: OCR paths
            _ocrModelPath = Path.Combine(baseDir, "models", "PlateDetectionCustomVN.onnx");
            _ocrConfigPath = Path.Combine(baseDir, "models", "VNCustomConfig.yaml");

            _outputDir = Path.Combine(baseDir, "detected_plates");
            _logDir = Path.Combine(baseDir, "logs");

            try
            {
                Directory.CreateDirectory(_outputDir);
                Directory.CreateDirectory(_logDir);
                Directory.CreateDirectory(Path.Combine(baseDir, "scripts"));
                Directory.CreateDirectory(Path.Combine(baseDir, "models"));

                LogInfo("PlateDetectionService initialized with OCR support");
                LogInfo($"Python: {_pythonExePath}");
                LogInfo($"Script: {_detectorScriptPath}");
                LogInfo($"YOLO Model: {_modelPath}");
                LogInfo($"OCR Model: {_ocrModelPath}");
                LogInfo($"OCR Config: {_ocrConfigPath}");
                LogInfo($"Output: {_outputDir}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to create directories: {ex.Message}");
            }
        }

        public static PlateDetectionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new PlateDetectionService();
                    }
                }
                return _instance;
            }
        }

        public bool ValidateConfiguration(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Kiểm tra Python
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _pythonExePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode != 0)
                    {
                        errorMessage = "Python không khả dụng. Vui lòng cài đặt Python 3.9+";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Không tìm thấy Python: {ex.Message}";
                return false;
            }

            // Kiểm tra script
            if (!File.Exists(_detectorScriptPath))
            {
                errorMessage = $"Không tìm thấy file script: {_detectorScriptPath}";
                return false;
            }

            // Kiểm tra YOLO model
            if (!File.Exists(_modelPath))
            {
                errorMessage = $"Không tìm thấy YOLO model: {_modelPath}";
                return false;
            }

            // ← THÊM MỚI: Kiểm tra OCR files (warning only, not error)
            if (!File.Exists(_ocrModelPath))
            {
                LogWarning($"OCR model not found: {_ocrModelPath} - OCR will be disabled");
            }

            if (!File.Exists(_ocrConfigPath))
            {
                LogWarning($"OCR config not found: {_ocrConfigPath} - OCR will be disabled");
            }

            return true;
        }

        public bool StartDetection(int cameraId, string rtspUrl, out string errorMessage)
        {
            errorMessage = string.Empty;
            lock (_lock)
            {
                if (_runningProcesses.ContainsKey(cameraId))
                {
                    var info = _runningProcesses[cameraId];
                    if (info.Process != null && !info.Process.HasExited)
                    {
                        errorMessage = $"Detection đã đang chạy cho camera {cameraId}";
                        LogWarning(errorMessage);
                        return false;
                    }
                    else
                        _runningProcesses.Remove(cameraId);
                }

                if (!ValidateConfiguration(out errorMessage))
                {
                    LogError(errorMessage);
                    return false;
                }

                try
                {
                    // ← CẬP NHẬT: Thêm OCR parameters
                    StringBuilder argsBuilder = new StringBuilder();
                    argsBuilder.Append($"\"{_detectorScriptPath}\" ");
                    argsBuilder.Append($"--camera_id {cameraId} ");
                    argsBuilder.Append($"--rtsp_url \"{rtspUrl}\" ");
                    argsBuilder.Append($"--model \"{_modelPath}\" ");
                    argsBuilder.Append($"--output \"{_outputDir}\" ");
                    //argsBuilder.Append($"--skip_frames 2 ");

                    // Add OCR parameters if files exist
                    if (File.Exists(_ocrModelPath) && File.Exists(_ocrConfigPath))
                    {
                        argsBuilder.Append($"--ocr_model \"{_ocrModelPath}\" ");
                        argsBuilder.Append($"--ocr_config \"{_ocrConfigPath}\" ");
                        LogInfo($"OCR enabled for camera {cameraId}");
                    }
                    else
                    {
                        LogInfo($"OCR disabled for camera {cameraId} (missing files)");
                    }

                    string arguments = argsBuilder.ToString();

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = _pythonExePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    };

                    Process process = new Process { StartInfo = startInfo };

                    ProcessInfo processInfo = new ProcessInfo
                    {
                        Process = process,
                        CameraId = cameraId,
                        RtspUrl = rtspUrl,
                        StartTime = DateTime.Now,
                        OutputLog = new StringBuilder(),
                        ErrorLog = new StringBuilder()
                    };

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            string msg = $"[Camera {cameraId}] {args.Data}";
                            Console.WriteLine(msg);
                            processInfo.OutputLog.AppendLine(args.Data);
                            LogInfo(msg);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            string msg = $"[Camera {cameraId} ERROR] {args.Data}";
                            Console.WriteLine(msg);
                            processInfo.ErrorLog.AppendLine(args.Data);
                            LogError(msg);
                        }
                    };

                    process.Exited += (sender, args) =>
                    {
                        try
                        {
                            int exitCode = process.HasExited ? process.ExitCode : -1;
                            LogWarning($"Detection process for camera {cameraId} exited with code {exitCode}");
                        }
                        catch
                        {
                            LogWarning($"Detection process for camera {cameraId} exited (unknown code)");
                        }
                    };
                    process.EnableRaisingEvents = true;

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    _runningProcesses[cameraId] = processInfo;
                    LogInfo($"Started detection with OCR for camera {cameraId}, RTSP: {rtspUrl}");
                    return true;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Lỗi khi start detection: {ex.Message}";
                    LogError(errorMessage);
                    return false;
                }
            }
        }

        public bool StopDetection(int cameraId, out string errorMessage)
        {
            errorMessage = string.Empty;
            lock (_lock)
            {
                if (!_runningProcesses.ContainsKey(cameraId))
                {
                    errorMessage = $"Không có detection nào đang chạy cho camera {cameraId}";
                    LogWarning(errorMessage);
                    return false;
                }

                try
                {
                    var info = _runningProcesses[cameraId];
                    var process = info.Process;

                    if (process != null && !process.HasExited)
                    {
                        try { process.Kill(); } catch { }
                        process.WaitForExit(3000);
                    }

                    process?.Dispose();
                    _runningProcesses.Remove(cameraId);

                    LogInfo($"Stopped detection for camera {cameraId}");
                    return true;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Lỗi khi stop detection: {ex.Message}";
                    LogError(errorMessage);
                    return false;
                }
            }
        }

        public bool IsDetectionRunning(int cameraId)
        {
            lock (_lock)
            {
                return _runningProcesses.ContainsKey(cameraId) &&
                       _runningProcesses[cameraId].Process != null &&
                       !_runningProcesses[cameraId].Process.HasExited;
            }
        }

        public List<int> GetRunningCameras()
        {
            lock (_lock)
            {
                return _runningProcesses
                    .Where(kvp => kvp.Value.Process != null && !kvp.Value.Process.HasExited)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        public string GetDetectionInfo(int cameraId)
        {
            lock (_lock)
            {
                if (!_runningProcesses.ContainsKey(cameraId))
                    return "Not running";

                var info = _runningProcesses[cameraId];
                TimeSpan runtime = DateTime.Now - info.StartTime;
                return $"Running for {runtime.TotalMinutes:F1} minutes\n" +
                       $"Started: {info.StartTime:HH:mm:ss}\n" +
                       $"RTSP: {info.RtspUrl}";
            }
        }

        public void StopAllDetections()
        {
            LogInfo("Stopping all detections...");
            lock (_lock)
            {
                foreach (var id in _runningProcesses.Keys.ToList())
                    StopDetection(id, out _);
            }
            LogInfo("All detections stopped");
        }

        public void Cleanup()
        {
            StopAllDetections();
        }

        private void LogInfo(string msg) => Log("INFO", msg);
        private void LogWarning(string msg) => Log("WARN", msg);
        private void LogError(string msg) => Log("ERROR", msg);

        private void Log(string level, string msg)
        {
            try
            {
                string file = Path.Combine(_logDir, $"detection_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] [{level}] {msg}\n");
            }
            catch { }
        }
    }
}