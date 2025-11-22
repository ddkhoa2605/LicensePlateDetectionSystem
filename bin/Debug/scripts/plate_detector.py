"""
YOLOv11 + FastPlateOCR License Plate Detection Service - Optimized
Phát hiện và nhận diện biển số xe từ RTSP stream
Sử dụng FastPlateOCR từ PlateWorker code với lọc trùng, quy tắc làm sạch biển số và quản lý database
"""

import cv2
import os
import sys
import re
import argparse
from datetime import datetime, timedelta
from pathlib import Path
import time
import numpy as np
import logging
from collections import defaultdict

# Import thư viện
try:
    from ultralytics import YOLO
    import pyodbc
    from fast_plate_ocr import LicensePlateRecognizer
except ImportError as e:
    print(f"Error importing required libraries: {e}")
    print("Please install: pip install ultralytics opencv-python pyodbc fast-plate-ocr")
    sys.exit(1)


class DatabaseManager:
    """Class quản lý kết nối và thao tác với database"""
    
    def __init__(self, connection_string):
        self.connection_string = connection_string
        self.logger = logging.getLogger(__name__)
    
    def get_connection(self):
        """Lấy kết nối database"""
        try:
            conn = pyodbc.connect(self.connection_string, timeout=5)
            return conn
        except Exception as e:
            self.logger.error(f"❌ Database connection error: {e}")
            return None
    
    def test_connection(self):
        """Test kết nối database"""
        conn = self.get_connection()
        if conn:
            try:
                cursor = conn.cursor()
                cursor.execute("SELECT @@VERSION")
                version = cursor.fetchone()
                self.logger.info(f"✅ Connected to SQL Server: {version[0][:50]}...")
                cursor.close()
                conn.close()
                return True
            except Exception as e:
                self.logger.error(f"❌ Database test error: {e}")
                return False
        return False
    
    def save_detection(self, camera_id, license_plate, image_path, confidence, 
                      ocr_confidence=None, is_valid=False):
        """
        Lưu kết quả phát hiện vào database
        
        Returns:
            True nếu lưu thành công
        """
        conn = self.get_connection()
        if not conn:
            return False
            
        try:
            cursor = conn.cursor()
            
            # Use 'None' as violation_type (compatible with DB constraint)
            violation_type = 'None'
            
            # Build extra_info JSON với validation status
            extra_info = {
                'yolo_confidence': float(confidence),
                'ocr_confidence': float(ocr_confidence) if ocr_confidence else None,
                'is_valid': is_valid,
                'validation_status': 'Valid' if is_valid else 'Invalid',
                'timestamp': datetime.now().isoformat()
            }
            extra_info_json = str(extra_info).replace("'", '"')
            
            # Gọi stored procedure
            cursor.execute("""
                EXEC sp_AddVehicleRecord 
                    @license_plate = ?,
                    @camera_id = ?,
                    @timestamp = ?,
                    @plate_image_path = ?,
                    @confidence = ?,
                    @violation_type = ?,
                    @extra_info = ?
            """, (
                license_plate,
                camera_id,
                datetime.now(),
                image_path,
                float(confidence),
                violation_type,
                extra_info_json
            ))
            
            conn.commit()
            cursor.close()
            conn.close()
            return True
            
        except Exception as e:
            self.logger.error(f"❌ DB Save Error: {e}")
            try:
                conn.close()
            except:
                pass
            return False
    
    def delete_old_records(self, days_old=30):
        """
        Xóa các record cũ hơn số ngày specified
        
        Args:
            days_old: Số ngày, các record cũ hơn số ngày này sẽ bị xóa
            
        Returns:
            Số lượng record đã xóa
        """
        conn = self.get_connection()
        if not conn:
            return 0
            
        try:
            cursor = conn.cursor()
            
            # Tính thời gian cutoff
            cutoff_date = datetime.now() - timedelta(days=days_old)
            
            # Đếm số record sẽ bị xóa
            cursor.execute("SELECT COUNT(*) FROM VehicleRecords WHERE timestamp < ?", cutoff_date)
            count_to_delete = cursor.fetchone()[0]
            
            if count_to_delete > 0:
                self.logger.info(f"🗑️  Deleting {count_to_delete} records older than {days_old} days...")
                
                # Xóa các record cũ
                cursor.execute("DELETE FROM VehicleRecords WHERE timestamp < ?", cutoff_date)
                conn.commit()
                
                self.logger.info(f"✅ Successfully deleted {count_to_delete} old records")
            else:
                self.logger.info("ℹ️  No old records to delete")
            
            cursor.close()
            conn.close()
            return count_to_delete
            
        except Exception as e:
            self.logger.error(f"❌ DB Delete Error: {e}")
            try:
                conn.close()
            except:
                pass
            return 0
    
    def delete_records_by_plate(self, license_plate):
        """
        Xóa các record theo biển số
        
        Args:
            license_plate: Biển số cần xóa
            
        Returns:
            Số lượng record đã xóa
        """
        conn = self.get_connection()
        if not conn:
            return 0
            
        try:
            cursor = conn.cursor()
            
            # Đếm số record sẽ bị xóa
            cursor.execute("SELECT COUNT(*) FROM VehicleRecords WHERE license_plate = ?", license_plate)
            count_to_delete = cursor.fetchone()[0]
            
            if count_to_delete > 0:
                self.logger.info(f"🗑️  Deleting {count_to_delete} records for plate: {license_plate}")
                
                # Xóa các record
                cursor.execute("DELETE FROM VehicleRecords WHERE license_plate = ?", license_plate)
                conn.commit()
                
                self.logger.info(f"✅ Successfully deleted {count_to_delete} records for plate: {license_plate}")
            else:
                self.logger.info(f"ℹ️  No records found for plate: {license_plate}")
            
            cursor.close()
            conn.close()
            return count_to_delete
            
        except Exception as e:
            self.logger.error(f"❌ DB Delete by Plate Error: {e}")
            try:
                conn.close()
            except:
                pass
            return 0
    
    def delete_all_records(self):
        """
        Xóa tất cả records trong bảng (CAUTION!)
        
        Returns:
            Số lượng record đã xóa
        """
        conn = self.get_connection()
        if not conn:
            return 0
            
        try:
            cursor = conn.cursor()
            
            # Đếm tổng số record
            cursor.execute("SELECT COUNT(*) FROM VehicleRecords")
            total_records = cursor.fetchone()[0]
            
            if total_records > 0:
                self.logger.warning(f"⚠️  DELETING ALL {total_records} RECORDS FROM DATABASE!")
                
                # Xóa tất cả record
                cursor.execute("DELETE FROM VehicleRecords")
                conn.commit()
                
                self.logger.warning(f"🗑️  Successfully deleted ALL {total_records} records from database!")
            else:
                self.logger.info("ℹ️  No records in database")
            
            cursor.close()
            conn.close()
            return total_records
            
        except Exception as e:
            self.logger.error(f"❌ DB Delete All Error: {e}")
            try:
                conn.close()
            except:
                pass
            return 0
    
    def get_database_stats(self):
        """
        Lấy thống kê database
        
        Returns:
            Dictionary chứa thống kê
        """
        conn = self.get_connection()
        if not conn:
            return {}
            
        try:
            cursor = conn.cursor()
            stats = {}
            
            # Tổng số records
            cursor.execute("SELECT COUNT(*) FROM VehicleRecords")
            stats['total_records'] = cursor.fetchone()[0]
            
            # Số records theo ngày
            cursor.execute("""
                SELECT CAST(timestamp as DATE) as date, COUNT(*) 
                FROM VehicleRecords 
                GROUP BY CAST(timestamp as DATE)
                ORDER BY date DESC
            """)
            stats['records_by_date'] = cursor.fetchall()
            
            # Số records theo biển số
            cursor.execute("""
                SELECT license_plate, COUNT(*) 
                FROM VehicleRecords 
                GROUP BY license_plate
                ORDER BY COUNT(*) DESC
            """)
            stats['records_by_plate'] = cursor.fetchall()
            
            # Record mới nhất
            cursor.execute("SELECT TOP 1 * FROM VehicleRecords ORDER BY timestamp DESC")
            latest_record = cursor.fetchone()
            if latest_record:
                stats['latest_record'] = {
                    'plate': latest_record.license_plate,
                    'timestamp': latest_record.timestamp,
                    'camera_id': latest_record.camera_id
                }
            
            cursor.close()
            conn.close()
            return stats
            
        except Exception as e:
            self.logger.error(f"❌ DB Stats Error: {e}")
            try:
                conn.close()
            except:
                pass
            return {}


class DuplicateFilter:
    """Class để lọc biển số trùng lặp"""
    
    def __init__(self, time_window=30, similarity_threshold=0.8):
        """
        Args:
            time_window: Thời gian (giây) để lọc trùng
            similarity_threshold: Ngưỡng similarity để coi là trùng
        """
        self.time_window = time_window
        self.similarity_threshold = similarity_threshold
        
        # Lưu lịch sử biển số: {plate_text: last_detection_time}
        self.plate_history = {}
        
        # Thống kê
        self.duplicates_filtered = 0
        
    def _calculate_similarity(self, str1, str2):
        """Tính độ tương đồng giữa 2 chuỗi"""
        if not str1 or not str2:
            return 0.0
        
        if str1 == str2:
            return 1.0
        
        # Simple similarity: matching characters / total characters
        matching = sum(c1 == c2 for c1, c2 in zip(str1, str2))
        total = max(len(str1), len(str2))
        return matching / total if total > 0 else 0
    
    def is_duplicate(self, plate_text, current_time):
        """
        Kiểm tra xem biển số có trùng trong time window không
        
        Returns:
            True nếu là trùng, False nếu là mới
        """
        if not plate_text or plate_text == "EMPTY":
            return False
        
        # Kiểm tra trùng chính xác
        if plate_text in self.plate_history:
            last_time = self.plate_history[plate_text]
            time_diff = current_time - last_time
            
            if time_diff < self.time_window:
                # Trùng chính xác trong time window
                self.duplicates_filtered += 1
                return True
        
        # Kiểm tra trùng tương tự (cho các OCR có kết quả hơi khác nhau)
        for existing_plate, last_time in list(self.plate_history.items()):
            time_diff = current_time - last_time
            if time_diff < self.time_window:
                similarity = self._calculate_similarity(plate_text, existing_plate)
                if similarity > self.similarity_threshold:
                    # Trùng tương tự trong time window
                    self.duplicates_filtered += 1
                    return True
        
        # Cập nhật lịch sử
        self.plate_history[plate_text] = current_time
        
        # Dọn dẹp lịch sử cũ
        self._clean_old_history(current_time)
        
        return False
    
    def _clean_old_history(self, current_time):
        """Xóa các bản ghi cũ hơn time window"""
        plates_to_remove = []
        for plate, last_time in self.plate_history.items():
            if current_time - last_time > self.time_window * 2:  # Giữ lâu hơn một chút
                plates_to_remove.append(plate)
        
        for plate in plates_to_remove:
            del self.plate_history[plate]
    
    def get_stats(self):
        """Lấy thống kê"""
        return {
            'active_plates': len(self.plate_history),
            'duplicates_filtered': self.duplicates_filtered
        }


class PlateDetector:
    """Class chính để phát hiện và nhận diện biển số xe với lọc trùng"""
    
    # Vietnamese license plate regex pattern (từ PlateWorker)
    VN_PLATE_PATTERN = re.compile(r'^(?=.{1,9}$)[0-9]{2}[A-Z]{1,2}[0-9]{4,6}$')
    
    def __init__(self, model_path, connection_string, output_dir="detected_plates",
                 ocr_model_path=None, ocr_config_path=None, enable_logging=True,
                 enable_duplicate_filter=True, duplicate_time_window=30,
                 auto_cleanup_days=0, save_only_valid_to_db=True):
        """
        Khởi tạo detector với YOLO và FastPlateOCR
        
        Args:
            model_path: Đường dẫn file model YOLOv11 (.pt)
            connection_string: Connection string SQL Server
            output_dir: Thư mục lưu ảnh biển số
            ocr_model_path: Đường dẫn ONNX model cho OCR
            ocr_config_path: Đường dẫn config file cho OCR
            enable_logging: Bật logging chi tiết
            enable_duplicate_filter: Bật lọc trùng
            duplicate_time_window: Thời gian lọc trùng (giây)
            auto_cleanup_days: Tự động xóa record cũ (0 = không xóa)
            save_only_valid_to_db: Chỉ lưu biển số hợp lệ vào database
        """
        # Setup logging
        if enable_logging:
            logging.basicConfig(
                level=logging.INFO,
                format="[%(levelname)s] %(asctime)s: %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
        self.logger = logging.getLogger(__name__)
        
        self.logger.info("="*60)
        self.logger.info("🚀 INITIALIZING PLATE DETECTOR WITH DATABASE MANAGEMENT")
        self.logger.info("="*60)
        self.logger.info(f"📁 YOLO Model: {model_path}")
        self.logger.info(f"📂 Output dir: {output_dir}")
        
        # Kiểm tra model tồn tại
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"Model file not found: {model_path}")
        
        # Load YOLO model
        self.logger.info("🔧 Loading YOLO model...")
        self.model = YOLO(model_path)
        self.logger.info("✅ YOLO model loaded successfully!")
        
        # Initialize FastPlateOCR (dùng logic từ PlateWorker) - FIXED VERSION
        if ocr_model_path and ocr_config_path:
            try:
                self.alpr = self._init_alpr(ocr_model_path, ocr_config_path)
                self.ocr_enabled = True
                self.logger.info("✅ OCR model loaded successfully!")
            except Exception as e:
                self.logger.error(f"❌ Failed to load OCR model: {e}")
                self.alpr = None
                self.ocr_enabled = False
        else:
            self.logger.warning("⚠️ OCR disabled (no model/config provided)")
            self.alpr = None
            self.ocr_enabled = False
        
        # Initialize Database Manager
        self.db_manager = DatabaseManager(connection_string)
        
        # Initialize duplicate filter
        self.enable_duplicate_filter = enable_duplicate_filter
        if enable_duplicate_filter:
            self.duplicate_filter = DuplicateFilter(
                time_window=duplicate_time_window,
                similarity_threshold=0.8
            )
            self.logger.info(f"✅ Duplicate filter initialized (time window: {duplicate_time_window}s)")
        else:
            self.duplicate_filter = None
            self.logger.info("⚠️ Duplicate filter disabled")
        
        # Auto cleanup
        self.auto_cleanup_days = auto_cleanup_days
        if auto_cleanup_days > 0:
            self.logger.info(f"🧹 Auto cleanup enabled: {auto_cleanup_days} days")
            self._auto_cleanup()
        else:
            self.logger.info("⚠️ Auto cleanup disabled")
        
        # Chỉ lưu biển số hợp lệ vào database
        self.save_only_valid_to_db = save_only_valid_to_db
        if save_only_valid_to_db:
            self.logger.info("💾 Database: Only saving VALID plates to database")
        else:
            self.logger.info("💾 Database: Saving ALL plates to database")
        
        # Lưu cấu hình
        self.connection_string = connection_string
        self.output_dir = output_dir
        
        # Tạo thư mục output với cấu trúc tối ưu
        self.reports_dir = os.path.join(output_dir, "reports")
        self.valid_dir = os.path.join(self.reports_dir, "Valid")
        self.invalid_dir = os.path.join(self.reports_dir, "Invalid")
        
        Path(self.valid_dir).mkdir(parents=True, exist_ok=True)
        Path(self.invalid_dir).mkdir(parents=True, exist_ok=True)
        self.logger.info("📁 Output directories ready")
        
        # Statistics
        self.total_detections = 0
        self.valid_plates = 0
        self.invalid_plates = 0
        self.total_frames = 0
        self.ocr_errors = 0
        self.duplicates_filtered = 0
        self.start_time = time.time()
        
        self.logger.info("="*60)
        self.logger.info("✅ INITIALIZATION COMPLETE")
        self.logger.info("="*60 + "\n")
    
    def _auto_cleanup(self):
        """Tự động xóa record cũ"""
        if self.auto_cleanup_days > 0:
            deleted_count = self.db_manager.delete_old_records(self.auto_cleanup_days)
            if deleted_count > 0:
                self.logger.info(f"✅ Auto cleanup completed: {deleted_count} records deleted")
    
    def _init_alpr(self, model_path, config_path):
        """
        Initialize FastPlateOCR recognizer (từ PlateWorker code) - FIXED VERSION
        
        Args:
            model_path: Đường dẫn ONNX model
            config_path: Đường dẫn config YAML
            
        Returns:
            LicensePlateRecognizer instance
        """
        model_path = Path(model_path)
        config_path = Path(config_path)

        if not model_path.exists():
            raise FileNotFoundError(f"OCR model not found: {model_path}")
        if not config_path.exists():
            raise FileNotFoundError(f"OCR config not found: {config_path}")

        self.logger.info(f"🧠 Loading FastPlateOCR model: {model_path}")
        self.logger.info(f"📄 Config: {config_path}")
        
        try:
            recognizer = LicensePlateRecognizer(
                onnx_model_path=str(model_path),
                plate_config_path=str(config_path),
                device="auto",
            )
            
            # Warm-up inference với ảnh test thực tế hơn (giống PlateWorker)
            self.logger.info("🔥 Warming up OCR model...")
            # Tạo ảnh test ngẫu nhiên thay vì zeros để test tốt hơn
            test_image = np.random.randint(0, 255, (96, 320, 3), dtype=np.uint8)
            result = recognizer.run(test_image, return_confidence=True)
            self.logger.info(f"✅ LicensePlateRecognizer warm-up successful: {result}")
            
            return recognizer
            
        except Exception as e:
            self.logger.error(f"❌ OCR initialization failed: {e}")
            raise
    
    @staticmethod
    def clean_text(s: str) -> str:
        """
        Làm sạch text biển số theo quy tắc từ PlateWorker
        - Bỏ dấu gạch dưới và khoảng trắng
        - Loại bỏ ký tự không hợp lệ
        - Loại bỏ các chữ cái dễ nhầm lẫn (O, I, W, Q, J)
        """
        if not s:
            return ""
        
        # Bỏ dấu gạch dưới và khoảng trắng
        s = s.replace('_', '').replace(' ', '').strip()
        
        # Loại bỏ ký tự không hợp lệ và các chữ cái dễ nhầm lẫn
        s = re.sub(r'[^A-Z0-9\-]|[OIWQJ]', '', s)
        
        return s
    
    def validate_plate_text(self, plate_text):
        """
        Validate biển số theo format Việt Nam (từ PlateWorker logic)
        
        Args:
            plate_text: Text biển số đã OCR
            
        Returns:
            True nếu hợp lệ, False nếu không
        """
        if not plate_text or len(plate_text) < 7:
            return False
        
        # Clean text theo quy tắc mới
        clean_text = self.clean_text(plate_text)
        
        # Check pattern
        is_valid = bool(self.VN_PLATE_PATTERN.match(clean_text))
        return is_valid
        
    def test_database_connection(self):
        """Test kết nối database"""
        return self.db_manager.test_connection()
    
    def save_detection_to_db(self, camera_id, license_plate, image_path, confidence, 
                            ocr_confidence=None, is_valid=False):
        """Lưu kết quả phát hiện vào database"""
        # Chỉ lưu vào database nếu biển số hợp lệ HOẶC nếu cấu hình cho phép lưu tất cả
        if self.save_only_valid_to_db and not is_valid:
            self.logger.debug(f"⏩ Skipping database save for invalid plate: {license_plate}")
            return True  # Trả về True để không ảnh hưởng đến flow chính
        
        return self.db_manager.save_detection(
            camera_id, license_plate, image_path, confidence, ocr_confidence, is_valid
        )
    
    # ========== DATABASE MANAGEMENT METHODS ==========
    
    def delete_old_records(self, days_old=30):
        """Xóa các record cũ"""
        return self.db_manager.delete_old_records(days_old)
    
    def delete_records_by_plate(self, license_plate):
        """Xóa các record theo biển số"""
        return self.db_manager.delete_records_by_plate(license_plate)
    
    def delete_all_records(self):
        """Xóa tất cả records (CAUTION!)"""
        return self.db_manager.delete_all_records()
    
    def get_database_stats(self):
        """Lấy thống kê database"""
        return self.db_manager.get_database_stats()
    
    def print_database_stats(self):
        """In thống kê database ra console"""
        stats = self.get_database_stats()
        
        self.logger.info("\n" + "="*60)
        self.logger.info("📊 DATABASE STATISTICS")
        self.logger.info("="*60)
        
        if not stats:
            self.logger.info("❌ No statistics available")
            return
        
        self.logger.info(f"📈 Total Records: {stats.get('total_records', 0)}")
        
        if 'latest_record' in stats:
            latest = stats['latest_record']
            self.logger.info(f"🕒 Latest Record: {latest.get('plate', 'N/A')} "
                           f"at {latest.get('timestamp', 'N/A')} "
                           f"(Camera: {latest.get('camera_id', 'N/A')})")
        
        # Hiển thị top biển số
        if 'records_by_plate' in stats and stats['records_by_plate']:
            self.logger.info("\n🏆 Top License Plates:")
            for plate, count in stats['records_by_plate'][:5]:  # Top 5
                self.logger.info(f"   {plate}: {count} records")
        
        # Hiển thị theo ngày
        if 'records_by_date' in stats and stats['records_by_date']:
            self.logger.info("\n📅 Recent Daily Records:")
            for date, count in stats['records_by_date'][:7]:  # 7 ngày gần nhất
                self.logger.info(f"   {date}: {count} records")
        
        self.logger.info("="*60)
    
    # ========== END DATABASE MANAGEMENT METHODS ==========
    
    def crop_and_save_plate(self, frame, box, camera_id, plate_text, is_valid):
        """
        Cắt và lưu ảnh biển số vào thư mục tương ứng
        Sử dụng logic sorting từ PlateWorker với tên thư mục đã được làm sạch
        
        Args:
            frame: Frame ảnh gốc (numpy array)
            box: Bounding box [x1, y1, x2, y2]
            camera_id: ID camera
            plate_text: Text biển số từ OCR
            is_valid: Biển số có hợp lệ không
            
        Returns:
            Đường dẫn file đã lưu
        """
        try:
            # Lấy tọa độ và đảm bảo trong phạm vi ảnh
            x1, y1, x2, y2 = map(int, box)
            h, w = frame.shape[:2]
            x1, y1 = max(0, x1), max(0, y1)
            x2, y2 = min(w, x2), min(h, y2)
            
            # Cắt ảnh biển số
            plate_img = frame[y1:y2, x1:x2]
            
            if plate_img.size == 0:
                self.logger.warning("⚠️ Empty plate image, skipping...")
                return None
            
            # Tạo tên file với timestamp chi tiết
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            
            # Xử lý plate_text (giống PlateWorker) với làm sạch tên thư mục
            norm_plate = plate_text if plate_text else "EMPTY"
            if norm_plate == "":
                norm_plate = "EMPTY"
            else:
                # Làm sạch tên thư mục: bỏ dấu gạch dưới và các ký tự không hợp lệ
                norm_plate = self.clean_text(norm_plate)
                if not norm_plate or norm_plate == "":
                    norm_plate = "INVALID"
            
            # Xác định thư mục lưu theo validation status (giống PlateWorker)
            if is_valid:
                dest_dir = Path(self.valid_dir) / norm_plate
            else:
                dest_dir = Path(self.invalid_dir) / norm_plate
            
            dest_dir.mkdir(parents=True, exist_ok=True)
            
            # Tạo filename
            filename = f"cam{camera_id}_{timestamp}.jpg"
            filepath = dest_dir / filename
            
            # Lưu ảnh
            cv2.imwrite(str(filepath), plate_img)
            
            return str(filepath)
            
        except Exception as e:
            self.logger.error(f"❌ Failed to crop/save plate: {e}")
            return None
    
    def run_ocr(self, plate_img):
        """
        Chạy OCR trên ảnh biển số (sử dụng logic từ PlateWorker) - FIXED VERSION
        
        Args:
            plate_img: Ảnh biển số đã crop (numpy array)
            
        Returns:
            (plate_text, confidence)
        """
        if not self.ocr_enabled or self.alpr is None:
            self.logger.debug("⚠️ OCR not enabled or model not loaded")
            return None, 0.0
        
        try:
            # Kiểm tra ảnh đầu vào
            if plate_img.size == 0:
                self.logger.debug("⚠️ Empty plate image for OCR")
                return None, 0.0
                
            # Resize nếu quá nhỏ - FIXED: Thêm kiểm tra kích thước tối thiểu
            h, w = plate_img.shape[:2]
            if h < 20 or w < 60:
                self.logger.debug(f"⚠️ Plate image too small for OCR: {w}x{h}")
                return None, 0.0
                
            if h < 50 or w < 100:
                scale = max(96/h, 320/w)
                new_h, new_w = int(h*scale), int(w*scale)
                plate_img = cv2.resize(plate_img, (new_w, new_h))
            
            # Chuẩn hóa định dạng ảnh đầu vào
            if len(plate_img.shape) == 2:
                plate_img = cv2.cvtColor(plate_img, cv2.COLOR_GRAY2BGR)
            elif plate_img.shape[2] == 4:
                plate_img = cv2.cvtColor(plate_img, cv2.COLOR_BGRA2BGR)
            
            # Run OCR (giống PlateWorker) - FIXED: Thêm debug logging
            self.logger.debug(f"Running OCR on image size: {plate_img.shape}")
            texts, confs = self.alpr.run(plate_img, return_confidence=True)
            
            # Extract plate và confidence (giống PlateWorker) - FIXED: Xử lý lỗi tốt hơn
            plate = texts[0] if texts else ""
            
            # FIX: Safe confidence extraction với debug
            if confs is not None and len(confs) > 0:
                try:
                    confidence = float(np.mean(confs[0]))
                except (TypeError, IndexError):
                    confidence = 0.0
            else:
                confidence = 0.0
            
            # Normalize plate (giống PlateWorker) và làm sạch text
            norm_plate = plate.upper().replace(" ", "")
            # Áp dụng làm sạch text cho kết quả OCR
            norm_plate = self.clean_text(norm_plate)
            
            self.logger.debug(f"OCR result: '{norm_plate}' with confidence: {confidence:.2f}")
            
            return norm_plate, confidence
                
        except Exception as e:
            self.logger.error(f"❌ OCR Error: {e}")
            self.ocr_errors += 1
            return None, 0.0
    
    def process_frame(self, frame, camera_id, min_confidence=0.35):
        """
        Xử lý một frame để phát hiện và nhận diện biển số với lọc trùng
        
        Args:
            frame: Frame ảnh từ video
            camera_id: ID camera
            min_confidence: Ngưỡng confidence tối thiểu cho YOLO
            
        Returns:
            (annotated_frame, num_detections, num_valid)
        """
        self.total_frames += 1
        current_time = time.time()
        
        # Detect với YOLO
        results = self.model(frame, verbose=False, conf=min_confidence)
        
        num_detections = 0
        num_valid = 0
        
        for result in results:
            boxes = result.boxes
            
            for box in boxes:
                yolo_confidence = float(box.conf[0])
                
                if yolo_confidence >= min_confidence:
                    # Lấy tọa độ
                    x1, y1, x2, y2 = map(int, box.xyxy[0])
                    
                    # Cắt ảnh biển số
                    h, w = frame.shape[:2]
                    x1_c, y1_c = max(0, x1), max(0, y1)
                    x2_c, y2_c = min(w, x2), min(h, y2)
                    cropped_plate = frame[y1_c:y2_c, x1_c:x2_c]
                    
                    if cropped_plate.size == 0:
                        continue
                    
                    # Run OCR with PlateWorker algorithm
                    plate_text, ocr_conf = self.run_ocr(cropped_plate)
                    
                    # Handle empty results (giống PlateWorker)
                    if not plate_text or plate_text == "":
                        plate_text = "EMPTY"
                        is_valid = False
                    else:
                        # Validate plate using PlateWorker algorithm với text đã làm sạch
                        clean_plate = self.clean_text(plate_text)
                        is_valid = bool(self.VN_PLATE_PATTERN.match(clean_plate))
                    
                    # Kiểm tra trùng với duplicate filter
                    is_duplicate = False
                    if self.enable_duplicate_filter and self.duplicate_filter and plate_text != "EMPTY":
                        is_duplicate = self.duplicate_filter.is_duplicate(plate_text, current_time)
                    
                    # Nếu là trùng, bỏ qua việc lưu và ghi log
                    if is_duplicate:
                        self.duplicates_filtered += 1
                        # Vẫn vẽ bounding box nhưng với màu khác
                        color = (128, 128, 128)  # Gray for duplicates
                        cv2.rectangle(frame, (x1, y1), (x2, y2), color, 2)
                        label = f"{plate_text} (DUP)"
                        cv2.putText(frame, label, (x1, y1 - 5),
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, color, 2)
                        continue
                    
                    # Save image with PlateWorker structure
                    image_path = self.crop_and_save_plate(
                        frame, (x1, y1, x2, y2), camera_id, plate_text, is_valid
                    )
                    
                    # Save to database - CHỈ lưu nếu biển số hợp lệ
                    if image_path:
                        success = self.save_detection_to_db(
                            camera_id, plate_text, image_path, 
                            yolo_confidence, ocr_conf, is_valid
                        )
                        
                        if success:
                            self.total_detections += 1
                            num_detections += 1
                            
                            if is_valid:
                                self.valid_plates += 1
                                num_valid += 1
                                
                                # Log với emoji status (giống PlateWorker) - CHỈ log biển số hợp lệ
                                self.logger.info(
                                    f"[Cam {camera_id}] ✅ VALID: {plate_text} "
                                    f"| YOLO: {yolo_confidence:.3f} | OCR: {ocr_conf:.2f}"
                                )
                            else:
                                self.invalid_plates += 1
                                # Chỉ log biển số không hợp lệ nếu không phải EMPTY
                                if plate_text != "EMPTY":
                                    self.logger.info(
                                        f"[Cam {camera_id}] ❌ INVALID: {plate_text} "
                                        f"| YOLO: {yolo_confidence:.3f} | OCR: {ocr_conf:.2f}"
                                    )
                    
                    # Draw bounding box với màu tùy validation
                    color = (0, 255, 0) if is_valid else (0, 0, 255)  # Green/Red
                    cv2.rectangle(frame, (x1, y1), (x2, y2), color, 2)
                    
                    # Draw label
                    if self.ocr_enabled and plate_text:
                        label = f"{plate_text} ({ocr_conf:.2f})"
                    else:
                        label = f"Plate {yolo_confidence:.2f}"
                    
                    # Background cho text
                    label_size, _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.6, 2)
                    cv2.rectangle(frame, (x1, y1 - label_size[1] - 10), 
                                  (x1 + label_size[0], y1), color, -1)
                    cv2.putText(frame, label, (x1, y1 - 5),
                               cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
        
        return frame, num_detections, num_valid
    
    def start_detection(self, rtsp_url, camera_id, skip_frames=0, 
                       min_confidence=0.35, save_annotated=False):
        """
        Bắt đầu phát hiện từ RTSP stream
        
        Args:
            rtsp_url: URL của RTSP stream
            camera_id: ID camera trong database
            skip_frames: Số frame bỏ qua giữa các lần detect
            min_confidence: Ngưỡng confidence tối thiểu (0.0-1.0)
            save_annotated: Lưu video đã annotate hay không
        """
        self.logger.info("\n" + "="*60)
        self.logger.info("🎬 STARTING DETECTION SERVICE WITH DATABASE MANAGEMENT")
        self.logger.info("="*60)
        self.logger.info(f"📹 Camera ID: {camera_id}")
        self.logger.info(f"🔗 RTSP URL: {rtsp_url}")
        self.logger.info(f"🧠 OCR: {'Enabled ✅' if self.ocr_enabled else 'Disabled ❌'}")
        self.logger.info(f"🚫 Duplicate Filter: {'Enabled ✅' if self.enable_duplicate_filter else 'Disabled ❌'}")
        self.logger.info(f"💾 Database: {'VALID plates only ✅' if self.save_only_valid_to_db else 'ALL plates 📝'}")
        self.logger.info(f"⏭️  Skip frames: {skip_frames}")
        self.logger.info(f"🎯 Min confidence: {min_confidence}")
        self.logger.info("="*60 + "\n")
        
        # Test database connection
        if not self.test_database_connection():
            self.logger.error(" Database connection failed. Exiting...")
            return
        
        # Mở video stream
        self.logger.info("🔌 Connecting to RTSP stream...")
        cap = cv2.VideoCapture(rtsp_url)
        
        # Set buffer size nhỏ để giảm lag
        cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        
        if not cap.isOpened():
            self.logger.error(f" Cannot open RTSP stream: {rtsp_url}")
            return
        
        # Lấy thông tin video
        fps = int(cap.get(cv2.CAP_PROP_FPS)) or 25
        width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        self.logger.info(f" Stream connected! FPS: {fps}, Resolution: {width}x{height}")
        
        # Video writer (nếu cần)
        video_writer = None
        if save_annotated:
            output_video = os.path.join(self.output_dir, f"camera_{camera_id}_annotated.mp4")
            fourcc = cv2.VideoWriter_fourcc(*'mp4v')
            video_writer = cv2.VideoWriter(output_video, fourcc, fps, (width, height))
            self.logger.info(f"💾 Saving annotated video to: {output_video}")
        
        frame_count = 0
        last_stats_time = time.time()
        reconnect_attempts = 0
        max_reconnect_attempts = 5
        
        self.logger.info("🏃 Detection loop started. Press Ctrl+C to stop.\n")
        
        try:
            while True:
                ret, frame = cap.read()
                
                if not ret:
                    reconnect_attempts += 1
                    if reconnect_attempts > max_reconnect_attempts:
                        self.logger.error(f"Max reconnection attempts ({max_reconnect_attempts}) reached. Stopping...")
                        break
                    
                    self.logger.warning(f"⚠️ Cannot read frame, reconnecting... (Attempt {reconnect_attempts}/{max_reconnect_attempts})")
                    time.sleep(2)
                    cap.release()
                    cap = cv2.VideoCapture(rtsp_url)
                    continue
                
                # Reset reconnect counter on successful read
                reconnect_attempts = 0
                
                # Chỉ detect mỗi N frames
                if frame_count % (skip_frames + 1) == 0:
                    processed_frame, num_detections, num_valid = self.process_frame(
                        frame, camera_id, min_confidence
                    )
                    
                    # Lưu video nếu cần
                    if video_writer:
                        video_writer.write(processed_frame)
                else:
                    processed_frame = frame
                
                frame_count += 1
                
                # In statistics mỗi 10 giây (giống PlateWorker style)
                if time.time() - last_stats_time >= 10:
                    elapsed = time.time() - self.start_time
                    avg_fps = frame_count / elapsed if elapsed > 0 else 0
                    
                    # Lấy thống kê duplicate filter
                    dup_stats = ""
                    if self.enable_duplicate_filter and self.duplicate_filter:
                        filter_stats = self.duplicate_filter.get_stats()
                        dup_stats = f" | Dup Filtered: {filter_stats['duplicates_filtered']} "
                    
                    self.logger.info(
                        f" [STATS] Frames: {frame_count} | "
                        f"Detections: {self.total_detections} | "
                        f"Valid: {self.valid_plates}  | "
                        f"Invalid: {self.invalid_plates}  | "
                        f"OCR Errors: {self.ocr_errors}"
                        f"{dup_stats} | "
                        f"Avg FPS: {avg_fps:.1f}"
                    )
                    last_stats_time = time.time()
                
        except KeyboardInterrupt:
            self.logger.info("\n⏹️ Detection stopped by user (Ctrl+C)")
        except Exception as e:
            self.logger.error(f"\n❌ Unexpected error: {e}")
            import traceback
            traceback.print_exc()
        finally:
            # Cleanup
            self.logger.info("\n🧹 Cleaning up resources...")
            cap.release()
            if video_writer:
                video_writer.release()
            
            # Final statistics (giống PlateWorker summary)
            elapsed = time.time() - self.start_time
            
            # Lấy thống kê duplicate filter
            dup_filtered_total = 0
            if self.enable_duplicate_filter and self.duplicate_filter:
                filter_stats = self.duplicate_filter.get_stats()
                dup_filtered_total = filter_stats['duplicates_filtered']
            
            self.logger.info("\n" + "="*60)
            self.logger.info("📊 DETECTION SUMMARY")
            self.logger.info("="*60)
            self.logger.info(f"⏱️  Runtime: {elapsed:.1f} seconds")
            self.logger.info(f"🎞️  Total frames processed: {self.total_frames}")
            self.logger.info(f"🚗 Total detections: {self.total_detections}")
            self.logger.info(f"✅ Valid plates: {self.valid_plates} ({self.valid_plates/self.total_detections*100 if self.total_detections > 0 else 0:.1f}%)")
            self.logger.info(f"❌ Invalid plates: {self.invalid_plates} ({self.invalid_plates/self.total_detections*100 if self.total_detections > 0 else 0:.1f}%)")
            self.logger.info(f"🚫 Duplicates filtered: {dup_filtered_total}")
            self.logger.info(f"⚠️  OCR errors: {self.ocr_errors}")
            self.logger.info(f"⚡ Average FPS: {self.total_frames/elapsed:.1f}")
            self.logger.info(f"🎯 Detection rate: {self.total_detections/self.total_frames*100 if self.total_frames > 0 else 0:.2f}% of frames")
            self.logger.info("="*60 + "\n")
            self.logger.info("✅ Finished OCR processing.")


def main():
    """Hàm main với argument parsing đầy đủ"""
    parser = argparse.ArgumentParser(
        description='YOLOv11 + FastPlateOCR License Plate Detector with Database Management',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Basic usage - only save valid plates to database
  python detect.py --camera_id 1 --rtsp_url "rtsp://..." --model yolo11.pt --ocr_model ocr.onnx --ocr_config config.yaml
  
  # Save all plates to database (including invalid ones)
  python detect.py --camera_id 1 --rtsp_url "rtsp://..." --model yolo11.pt --ocr_model ocr.onnx --ocr_config config.yaml --save_all_to_db
  
  # Auto cleanup old records
  python detect.py --camera_id 1 --rtsp_url "rtsp://..." --model yolo11.pt --ocr_model ocr.onnx --ocr_config config.yaml --auto_cleanup_days 30
  
  # Disable duplicate filter
  python detect.py --camera_id 1 --rtsp_url "rtsp://..." --model yolo11.pt --ocr_model ocr.onnx --ocr_config config.yaml --no_duplicate_filter
        """
    )
    
    parser.add_argument('--camera_id', type=int, required=True,
                       help='Camera ID in database')
    parser.add_argument('--rtsp_url', type=str, required=True,
                       help='RTSP stream URL')
    parser.add_argument('--model', type=str, required=True,
                       help='Path to YOLOv11 model file (.pt)')
    parser.add_argument('--ocr_model', type=str, required=True,
                       help='Path to OCR ONNX model')
    parser.add_argument('--ocr_config', type=str, required=True,
                       help='Path to OCR config YAML')
    parser.add_argument('--output', type=str, default='detected_plates',
                       help='Output directory for saved images (default: detected_plates)')
    parser.add_argument('--skip_frames', type=int, default=0,
                       help='Process every N frames (default: 0)')
    parser.add_argument('--min_confidence', type=float, default=0.35,
                       help='Minimum confidence threshold 0.0-1.0 (default: 0.35)')
    parser.add_argument('--save_video', action='store_true',
                       help='Save annotated video')
    parser.add_argument('--db_server', type=str, default='.',
                       help='SQL Server instance (default: .)')
    parser.add_argument('--db_name', type=str, default='QuanLyGiaoThong',
                       help='Database name (default: QuanLyGiaoThong)')
    parser.add_argument('--no_duplicate_filter', action='store_true',
                       help='Disable duplicate filtering')
    parser.add_argument('--duplicate_window', type=int, default=30,
                       help='Time window for duplicate filtering in seconds (default: 30)')
    parser.add_argument('--auto_cleanup_days', type=int, default=0,
                       help='Auto delete records older than X days (0 = disabled, default: 0)')
    parser.add_argument('--save_all_to_db', action='store_true',
                       help='Save ALL plates to database (including invalid ones). Default: only valid plates')
    
    args = parser.parse_args()
    
    # Connection string
    CONNECTION_STRING = (
        f"Driver={{SQL Server}};"
        f"Server={args.db_server};"
        f"Database={args.db_name};"
        f"Trusted_Connection=yes;"
    )
    
    try:
        # Khởi tạo detector với database management
        detector = PlateDetector(
            model_path=args.model,
            connection_string=CONNECTION_STRING,
            output_dir=args.output,
            ocr_model_path=args.ocr_model,
            ocr_config_path=args.ocr_config,
            enable_logging=True,
            enable_duplicate_filter=not args.no_duplicate_filter,
            duplicate_time_window=args.duplicate_window,
            auto_cleanup_days=args.auto_cleanup_days,
            save_only_valid_to_db=not args.save_all_to_db  # Đảo ngược logic
        )
        
        # Bắt đầu detection
        detector.start_detection(
            args.rtsp_url,
            args.camera_id,
            skip_frames=args.skip_frames,
            min_confidence=args.min_confidence,
            save_annotated=args.save_video
        )
        
    except Exception as e:
        print(f"\nFATAL ERROR: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()