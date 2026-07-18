# Screw AOI Inspection System

使用 C# 與 OpenCvSharp 建立規則式 AOI 影像檢測流程。

本專案模擬固定治具與固定拍攝條件下的螺絲外觀檢測，透過影像處理取得工件輪廓、校正方向、分離螺帽與螺牙區域，並利用尺寸量測結果進行缺陷判定。

## Processing Pipeline

Image Input  
↓  
Gray Scale  
↓  
Bilateral Filter  
↓  
Otsu Threshold  
↓  
Morphology Processing  
↓  
Contour Detection  
↓  
FitLine Orientation Correction  
↓  
Nut / Thread Segmentation  
↓  
Feature Measurement  
↓  
PASS / FAIL


## Features

- 使用 OpenCV 進行影像前處理與二值化
- 利用輪廓分析取得工件外形
- 使用 FitLine 修正物件方向
- 分別量測螺帽與螺牙尺寸
- 根據標準樣本進行規則式缺陷判定


## Simulation Conditions

為模擬穩定產線環境：

- 固定相機與光源條件
- 固定影像解析度 1000×1000
- 工件以固定方向進入檢測區
- 不考慮相機校正與複雜定位演算法


## Technology

- C#
- OpenCvSharp
- Visual Studio
