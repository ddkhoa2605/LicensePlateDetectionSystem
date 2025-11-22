# License Plate Detection & Recognition System (Vietnam – OCR Optimized)

A **Windows Forms (C#)** application for **real-time Vietnamese license plate detection and OCR recognition**.  
The system supports webcam/IP camera streaming, image upload, automatic plate cropping, high-accuracy OCR decoding, and result storage for traffic and vehicle monitoring purposes.

---

## Features

### Real-time Detection
- Real-time license plate detection using **retrained YOLO**
- Optimized for real-world scenarios

### OCR Recognition
- **Fast-Plate OCR (finetuned)** for Vietnamese license plate fonts
- Supports:
  - Motorcycle
  - Car plates
  - Outdoor conditions: blur, motion, low-light, shadows

### Input Sources
- USB camera / IP camera livestream
- Upload local images
- Automatic plate cropping and saving

### Additional Features
- Detection and OCR history logging
- Vehicle information lookup (optional)
- Integrated **ONNX Runtime** for fast CPU inference
- **Streamlit Demo App** for quick model testing in Python

---

## Technologies Used

### Detection
- YOLO (fine-tuned)
- Custom dataset including:
  - Multi-angle plates
  - Motorbike and car plates
  - Harsh lighting: rain, glare, blur

### OCR Recognition
- Fast-Plate OCR + ONNX Runtime
- Finetuned on:
  - Vietnamese license plate fonts
  - Different plate layouts
  - Real-world lighting conditions

### Application Framework
- **C# .NET – Windows Forms**
- **OpenCV / EmguCV**
- **ONNX Runtime**
- **YAML** configuration for plate formatting rules
- **Streamlit + Python** for demo/testing

---

## Project Structure
/detected_plates # Saved cropped license plate images
/appStreamlit.py # Streamlit demo app
/cct_s_v1_vn.onnx # OCR model (finetuned)
/DetectLicense_YOLO.pt # YOLO detection model
/cct_s_v1_vn_plate_config.yaml # Vietnamese plate formatting rules
/PlateDetectionService.cs # C# detection + OCR inference service
/camgiamSat.cs # Live camera module
/truyxuatthongtinxe.cs # Vehicle information lookup module


---

## How to Run (C# Application)

1. **Clone the repository**
2. Open `Plate_recor_Final.sln` using **Visual Studio**
3. Restore all NuGet packages
4. Ensure the following files are placed in the project:
   - `DetectLicense_YOLO.pt`
   - `cct_s_v1_vn.onnx`
   - `cct_s_v1_vn_plate_config.yaml`
5. Press **Start (F5)**

The camera feed will start → YOLO detects plates → cropped plates are saved → OCR extracts text.

---

## How to Run (Streamlit Demo)
pip install streamlit fast-plate-ocr onnxruntime opencv-python
streamlit run appStreamlit.py

<img width="1919" height="948" alt="image" src="https://github.com/user-attachments/assets/e3ed5826-e341-48a2-981f-2cec0b0aa2fb" />
<img width="1896" height="856" alt="image" src="https://github.com/user-attachments/assets/67a03dc5-a51b-4553-a391-a5a321eda8eb" />
<img width="1857" height="810" alt="image" src="https://github.com/user-attachments/assets/4c5d6564-f8db-4007-9fbf-efb45b257b60" />


## 📸 Demo Screenshots




