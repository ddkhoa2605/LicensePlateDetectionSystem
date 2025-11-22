"""
Streamlit App - License Plate Detection
Upload video và nhận diện biển số xe
"""

import streamlit as st
import cv2
import tempfile
import numpy as np
from pathlib import Path
import re
from datetime import datetime
import time

# Import thư viện cần thiết
try:
    from ultralytics import YOLO
    from fast_plate_ocr import LicensePlateRecognizer
except ImportError as e:
    st.error(f"Missing libraries: {e}")
    st.info("Install: pip install ultralytics opencv-python fast-plate-ocr streamlit")
    st.stop()

# ================== CẤU HÌNH CỐ ĐỊNH ==================
CONFIG = {
    'model_path': r"d:\Project\Bien so xe\Plate_recor_Final\Plate_recor_Final\DetectLisence_YOLO11.pt",
    'ocr_model_path': r"D:\Project\Bien so xe\Plate_recor_Final\Plate_recor_Final\cct_s_v1_vn.onnx",
    'ocr_config_path': r"D:\Project\Bien so xe\Plate_recor_Final\Plate_recor_Final\cct_s_v1_vn_plate_config.yaml",
    'min_confidence': 0.35,
    'skip_frames': 5,
    'show_video': True
}
# =====================================================


class SimplePlateDetector:
    """Class đơn giản để phát hiện biển số"""
    
    VN_PLATE_PATTERN = re.compile(r'^(?=.{1,9}$)[0-9]{2}[A-Z]{1,2}[0-9]{4,6}$')
    
    def __init__(self, model_path, ocr_model_path=None, ocr_config_path=None):
        self.model = YOLO(model_path)
        
        # Initialize OCR nếu có
        if ocr_model_path and ocr_config_path:
            self.alpr = LicensePlateRecognizer(
                onnx_model_path=str(ocr_model_path),
                plate_config_path=str(ocr_config_path),
                device="cuda" if self._check_cuda() else "cpu"
            )
            self.ocr_enabled = True
        else:
            self.alpr = None
            self.ocr_enabled = False
    
    def _check_cuda(self):
        """Check CUDA availability"""
        try:
            import torch
            return torch.cuda.is_available()
        except:
            return False
    
    @staticmethod
    def clean_text(s: str) -> str:
        """Làm sạch text biển số"""
        if not s:
            return ""
        s = s.replace('_', '').replace(' ', '').strip()
        s = re.sub(r'[^A-Z0-9\-]|[OIWQJ]', '', s)
        return s
    
    def validate_plate(self, plate_text):
        """Validate biển số Việt Nam"""
        if not plate_text or len(plate_text) < 7:
            return False
        clean = self.clean_text(plate_text)
        return bool(self.VN_PLATE_PATTERN.match(clean))
    
    def run_ocr(self, plate_img):
        """Chạy OCR trên ảnh biển số"""
        if not self.ocr_enabled:
            return None, 0.0
        
        try:
            h, w = plate_img.shape[:2]
            if h < 20 or w < 60:
                return None, 0.0
            
            if h < 50 or w < 100:
                scale = max(96/h, 320/w)
                plate_img = cv2.resize(plate_img, (int(w*scale), int(h*scale)))
            
            if len(plate_img.shape) == 2:
                plate_img = cv2.cvtColor(plate_img, cv2.COLOR_GRAY2BGR)
            
            texts, confs = self.alpr.run(plate_img, return_confidence=True)
            plate = texts[0] if texts else ""
            
            if confs is not None and len(confs) > 0:
                confidence = float(np.mean(confs[0]))
            else:
                confidence = 0.0
            
            norm_plate = self.clean_text(plate.upper().replace(" ", ""))
            return norm_plate, confidence
            
        except Exception as e:
            return None, 0.0
    
    def process_frame(self, frame, min_confidence=0.35):
        """Xử lý một frame"""
        results = self.model(frame, verbose=False, conf=min_confidence)
        
        detections = []
        annotated_frame = frame.copy()
        
        for result in results:
            boxes = result.boxes
            
            for box in boxes:
                conf = float(box.conf[0])
                
                if conf >= min_confidence:
                    x1, y1, x2, y2 = map(int, box.xyxy[0])
                    
                    # Crop biển số
                    h, w = frame.shape[:2]
                    x1_c = max(0, x1)
                    y1_c = max(0, y1)
                    x2_c = min(w, x2)
                    y2_c = min(h, y2)
                    
                    cropped = frame[y1_c:y2_c, x1_c:x2_c]
                    
                    if cropped.size == 0:
                        continue
                    
                    # OCR
                    plate_text, ocr_conf = self.run_ocr(cropped)
                    
                    if not plate_text:
                        plate_text = "UNKNOWN"
                        is_valid = False
                    else:
                        is_valid = self.validate_plate(plate_text)
                    
                    # Lưu thông tin
                    detections.append({
                        'plate': plate_text,
                        'confidence': conf,
                        'ocr_confidence': ocr_conf,
                        'is_valid': is_valid,
                        'bbox': (x1, y1, x2, y2),
                        'image': cropped
                    })
                    
                    # Vẽ bounding box
                    color = (0, 255, 0) if is_valid else (0, 0, 255)
                    cv2.rectangle(annotated_frame, (x1, y1), (x2, y2), color, 2)
                    
                    # Label
                    label = f"{plate_text} ({ocr_conf:.2f})" if plate_text != "UNKNOWN" else f"Plate ({conf:.2f})"
                    
                    # Background cho text
                    (label_w, label_h), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.6, 2)
                    cv2.rectangle(annotated_frame, (x1, y1 - label_h - 10), 
                                (x1 + label_w, y1), color, -1)
                    cv2.putText(annotated_frame, label, (x1, y1 - 5),
                              cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
        
        return annotated_frame, detections


def main():
    st.set_page_config(
        page_title="License Plate Detection",
        page_icon="🚗",
        layout="wide"
    )
    
    st.title("License Plate Detection System")
    st.markdown("Upload video để nhận diện biển số xe")
    
    # Upload video
    st.header("📁 Upload Video")
    uploaded_file = st.file_uploader(
        "Choose a video file",
        type=['mp4', 'avi', 'mov', 'mkv'],
        help="Chọn file video để phát hiện biển số"
    )
    
    if uploaded_file is not None:
        # Kiểm tra model
        if not Path(CONFIG['model_path']).exists():
            st.error(f"Model file not found: {CONFIG['model_path']}")
            st.stop()
        
        # Lưu video tạm
        tfile = tempfile.NamedTemporaryFile(delete=False, suffix='.mp4')
        tfile.write(uploaded_file.read())
        video_path = tfile.name
        
        # Initialize detector
        with st.spinner("Loading models..."):
            try:
                # Check OCR paths
                ocr_model = CONFIG['ocr_model_path'] if CONFIG['ocr_model_path'] and Path(CONFIG['ocr_model_path']).exists() else None
                ocr_config = CONFIG['ocr_config_path'] if CONFIG['ocr_config_path'] and Path(CONFIG['ocr_config_path']).exists() else None
                
                if ocr_model and ocr_config:
                    st.info("OCR enabled")
                else:
                    st.warning("OCR disabled (model/config not found)")
                
                detector = SimplePlateDetector(
                    model_path=CONFIG['model_path'],
                    ocr_model_path=ocr_model,
                    ocr_config_path=ocr_config
                )
                st.success("Models loaded successfully!")
                
            except Exception as e:
                st.error(f"Failed to load models: {e}")
                st.stop()
        
        # Process video button
        if st.button("Start Detection!", type="primary"):
            # Tạo layout
            col1, col2 = st.columns([2, 1])
            
            with col1:
                st.subheader("📹 Video Processing")
                video_placeholder = st.empty()
                progress_bar = st.progress(0)
                status_text = st.empty()
            
            with col2:
                st.subheader("🎯 Detected Plates")
                detections_container = st.container()
            
            # Open video
            cap = cv2.VideoCapture(video_path)
            total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
            fps = int(cap.get(cv2.CAP_PROP_FPS)) or 25
            
            all_detections = []
            frame_count = 0
            processed_count = 0
            
            start_time = time.time()
            
            try:
                while cap.isOpened():
                    ret, frame = cap.read()
                    if not ret:
                        break
                    
                    frame_count += 1
                    
                    # Process frame
                    if frame_count % CONFIG['skip_frames'] == 0:
                        annotated_frame, detections = detector.process_frame(
                            frame, CONFIG['min_confidence']
                        )
                        
                        processed_count += 1
                        
                        # Lưu detections
                        for det in detections:
                            det['frame'] = frame_count
                            det['timestamp'] = frame_count / fps
                            all_detections.append(det)
                        
                        # Hiển thị frame
                        if CONFIG['show_video']:
                            # Convert BGR to RGB
                            rgb_frame = cv2.cvtColor(annotated_frame, cv2.COLOR_BGR2RGB)
                            video_placeholder.image(
                                rgb_frame,
                                channels="RGB",
                                use_container_width=True
                            )
                    
                    # Update progress
                    progress = frame_count / total_frames
                    progress_bar.progress(progress)
                    status_text.text(
                        f"Frame: {frame_count}/{total_frames} | "
                        f"Processed: {processed_count} | "
                        f"Detections: {len(all_detections)}"
                    )
                
                cap.release()
                
                # Processing complete
                elapsed_time = time.time() - start_time
                
                st.success(f"Processing complete in {elapsed_time:.1f}s!")
                st.info(
                    f"**Summary:** {frame_count} frames processed, "
                    f"{len(all_detections)} plates detected"
                )
                
                # Display results
                if all_detections:
                    st.header("Detection Results")
                    
                    # Tabs cho kết quả
                    tab1, tab2, tab3 = st.tabs(["Summary", "All Detections", "Valid Plates Only"])
                    
                    with tab1:
                        # Statistics
                        valid_plates = [d for d in all_detections if d['is_valid']]
                        unique_plates = list(set([d['plate'] for d in valid_plates]))
                        
                        col1, col2, col3, col4 = st.columns(4)
                        col1.metric("Total Detections", len(all_detections))
                        col2.metric("Valid Plates", len(valid_plates))
                        col3.metric("Unique Plates", len(unique_plates))
                        col4.metric("Accuracy", f"{len(valid_plates)/len(all_detections)*100:.1f}%")
                        
                        # Plate frequency
                        if unique_plates:
                            st.subheader("Plate Frequency")
                            plate_counts = {}
                            for plate in unique_plates:
                                count = sum(1 for d in valid_plates if d['plate'] == plate)
                                plate_counts[plate] = count
                            
                            sorted_plates = sorted(plate_counts.items(), key=lambda x: x[1], reverse=True)
                            
                            for plate, count in sorted_plates[:10]:
                                st.write(f"**{plate}**: {count} times")
                    
                    with tab2:
                        # All detections
                        st.subheader("All Detected Plates")
                        
                        for i, det in enumerate(all_detections):
                            with st.expander(
                                f"Detection #{i+1} - {det['plate']} "
                                f"{'✅' if det['is_valid'] else '❌'} "
                                f"(Frame {det['frame']})"
                            ):
                                col1, col2 = st.columns([1, 2])
                                
                                with col1:
                                    st.image(
                                        cv2.cvtColor(det['image'], cv2.COLOR_BGR2RGB),
                                        caption=det['plate'],
                                        use_container_width=True
                                    )
                                
                                with col2:
                                    st.write(f"**Plate:** {det['plate']}")
                                    st.write(f"**Status:** {'Valid ✅' if det['is_valid'] else 'Invalid ❌'}")
                                    st.write(f"**Frame:** {det['frame']}")
                                    st.write(f"**Time:** {det['timestamp']:.2f}s")
                                    st.write(f"**YOLO Confidence:** {det['confidence']:.3f}")
                                    st.write(f"**OCR Confidence:** {det['ocr_confidence']:.3f}")
                    
                    with tab3:
                        # Valid plates only - LỌC TRÙNG
                        st.subheader("Valid Plates Only (Unique)")
                        
                        if valid_plates:
                            # Lọc trùng: Giữ lại detection đầu tiên của mỗi biển số
                            unique_valid_plates = {}
                            for det in valid_plates:
                                plate_num = det['plate']
                                if plate_num not in unique_valid_plates:
                                    unique_valid_plates[plate_num] = det
                            
                            st.info(f"Found {len(unique_valid_plates)} unique valid plates (filtered from {len(valid_plates)} total valid detections)")
                            
                            for i, (plate_num, det) in enumerate(unique_valid_plates.items()):
                                with st.expander(
                                    f"{det['plate']} (First seen: Frame {det['frame']})"
                                ):
                                    col1, col2 = st.columns([1, 2])
                                    
                                    with col1:
                                        st.image(
                                            cv2.cvtColor(det['image'], cv2.COLOR_BGR2RGB),
                                            caption=det['plate'],
                                            use_container_width=True
                                        )
                                    
                                    with col2:
                                        st.write(f"**Plate:** {det['plate']}")
                                        st.write(f"**Frame:** {det['frame']}")
                                        st.write(f"**Time:** {det['timestamp']:.2f}s")
                                        st.write(f"**YOLO Confidence:** {det['confidence']:.3f}")
                                        st.write(f"**OCR Confidence:** {det['ocr_confidence']:.3f}")
                                        
                                        # Đếm số lần xuất hiện
                                        count = sum(1 for d in valid_plates if d['plate'] == plate_num)
                                        st.write(f"**Total appearances:** {count} times")
                        else:
                            st.warning("No valid plates detected")
                
                else:
                    st.warning("No plates detected in the video")
                
            except Exception as e:
                st.error(f"Error during processing: {e}")
                cap.release()
    
    else:
        st.info("Please upload a video file to start detection!!!")
    
    # Footer
    st.markdown("---")
    st.markdown(
        """
        <div style='text-align: center'>
            <p>License Plate Detection System | Powered by YOLOv11 + FastPlateOCR</p>
        </div>
        """,
        unsafe_allow_html=True
    )


if __name__ == "__main__":
    main()