using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using System.Diagnostics.CodeAnalysis;//畫excel用


class program {
    //Mat物件宣告
    static Mat original;
    static Mat kernel;
    static Mat gray;
    static Mat bilateral;
    static Mat otsu;
    static Mat closing;

    static Point[][] contours;//輪廓們
    static HierarchyIndex[] hierarchy;
    static Mat contourImage;//存所有輪廓的圖


    static Point[] mainContour;//最大輪廓
    static Rect mainRect;//最大輪廓的外接矩形

    static Mat filledMask;//存塗滿輪廓的圖

    static Mat boundingImage;//存外接矩形的圖

    static RotatedRect mainRotatedRect;//最小外接旋轉矩形
    static Mat minAreaRectImage;//存最小外接旋轉矩形的圖

    static Mat rotatedImage;//存旋轉後的圖

    static Point[][] rotatedContours;//旋轉後的輪廓們
    static HierarchyIndex[] rotatedHierarchy;
    static Point[] rotatedMainContour;//旋轉後的最大輪廓
    static Rect rotatedBoundingRect;//旋轉後的最大輪廓外接矩形
    static Mat rotatedContourImage;//存旋轉後的最大輪廓外接矩形的圖

    static Mat fitlineRotateResult;//儲存fitline旋轉後的圖
    static Point[][] fitlineRotatedContours;//旋轉後的輪廓們
    static HierarchyIndex[] fitlineRotatedHierarchy;
    static Point[] fitlineRotatedMainContour;//旋轉後的最大輪廓
    static Rect fitlineRotatedBoundingRect;//旋轉後的最大輪廓外接矩形
    static Mat fitlineRotatedContourImage;//存旋轉後的圖

    static Mat roiImage;//存ROI後的圖

    static Mat nut;//存螺帽的圖
    static Point[] nutMainContour;//螺帽輪廓
    static Mat nutContourImage;//存螺帽輪廓的圖
    static Rect nutMainRect;//存螺帽的外接矩型
    static Mat nutMainRectImage;//存螺帽的外接矩型的圖

    static Mat thread;//存螺牙的圖
    static Point[] threadMainContour;//螺牙輪廓
    static Mat threadContourImage;//存螺牙的輪廓
    static Rect threadMainRect;//存螺牙的外接矩型
    static Mat threadMainRectImage;//存螺牙的外接矩型的圖

    static MeasurementResult correctSample = new MeasurementResult();


    static Mat threadShortSample;//螺牙太短

    static Point[]  compareImageMainContour;//最大輪廓
    static Mat compareImageContourImage;//存螺牙的輪廓
    static Rect compareImageMainRect;//存螺牙的外接矩型
    static Mat compareImageMainRectImage;//存螺牙的外接矩型的圖

    public class MeasurementResult
    {
        //存放測量結果
        public double headWidth { get; set; }
        public double headHeight { get; set; }
        public double headArea { get; set; }
        public double headPerimeter { get; set; }

        public double threadLength { get; set; }
        public double threadWidth { get; set; }
        public double threadArea { get; set; }
        public double threadPerimeter { get; set; }
    }

    static void Main()
    {
        System.Console.WriteLine("<---簡化模擬環境--->");
        System.Console.WriteLine("1. 固定工業相機拍攝");
        System.Console.WriteLine("2. 固定光源與曝光條件");
        System.Console.WriteLine("3. 所有影像解析度皆為 1000×1000");
        System.Console.WriteLine("4. 工件經治具定位，以相同方向送入檢測站");
        System.Console.WriteLine("5. 聚焦於規則式 AOI 的影像處理與尺寸量測，不討論相機校正與定位演算法");
        System.Console.WriteLine("<---模擬建立螺絲工件正確樣本--->");
        System.Console.WriteLine("<---載入圖片--->");
        original = Cv2.ImRead("original.jpg");
        System.Console.WriteLine("<---執行影像處理流程--->");
        CreateKernel();//呼叫後未來的openCV函式都可共用
        doGray();
        doBilateralFilter();
        doOtsu();
        doClosing();
        //先抓輪廓才能得知外接矩形
        doFindContours();
        CatchMaxContour();
        fullTheHole();//根據輪廓把洞填滿
        System.Console.WriteLine("<---已獲取圖片中的物件--->");
        doBoundingRect();
        getMinAreaRect();//最小外接旋轉矩形
        doRotated();//透過旋轉把圖片擺正
        getRotatedBoundingRect();//取得旋轉後的最大輪廓和座標
        System.Console.WriteLine("但是，這是根據最小旋轉外接矩形來判斷怎麼旋轉。不符合人眼中的水平。");
        System.Console.WriteLine("所以改用FitLine會更符合這種不規則形狀的工件");
        doFitLine();//發現最小外接旋轉矩形不如預期的是水平，故改尋找物件主軸角度
        System.Console.WriteLine("主軸雖然有些不夠完美水平，但人為檢測後認為屬於容許誤差範圍");
        System.Console.WriteLine("<---執行物件測量前處理--->");
        getFitlineRotatedBoundingRect();
        System.Console.WriteLine("<---區分螺帽跟螺牙的界線並各自測量--->");
        roiRotatedImage();//ROI
        //抓ROI後的輪廓
        scanRotatedImage();//沿著X 軸從左到右掃描圖片並匯出CSV，接著用人去分析
        cutImage();//切開螺帽和螺牙兩個區域
        measuringNut();
        measuringThread();
        getObjectInfo();
        System.Console.WriteLine("<---已建立正確樣本的基本資訊--->");
        System.Console.WriteLine("<---模擬缺陷品樣本--->");
        createBadSample();//thread_short.jpg

        //Grayscale，因為ImRead預設是彩色圖，但是我找輪廓需要黑白圖
        threadShortSample = Cv2.ImRead("test_img/thread_short.jpg",ImreadModes.Grayscale);

        compareCenter(thread);
        //compareCenter(threadShortSample);
    }

    //kernal共用設置
    static void CreateKernel()
    {
        kernel = Cv2.GetStructuringElement(
            MorphShapes.Rect,
            new Size(3, 3));
    }

    //第一階段-前處理
    //前處理-灰階
    static void doGray() {
        gray = new Mat();
        System.Console.WriteLine("doGray");
        Cv2.CvtColor(original, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.ImWrite("test_img/1-gray.jpg", gray);
    }

    //前處理-濾波
    static void doBilateralFilter() {
        //讓螺絲和背景的界線更穩定。
        bilateral = new Mat();
        System.Console.WriteLine("doBilateraFilter");
        Cv2.BilateralFilter(gray, bilateral, 9, 75, 75);
        Cv2.ImWrite("test_img/2-bilateral.jpg", bilateral);
    }

    //影像分割
    static void doOtsu()
    {
        System.Console.WriteLine("doOtsu");
        //自動 threshold + 黑白反轉
        otsu = new Mat();
        Cv2.Threshold(bilateral, otsu, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);//"|"表示同時啟用
        Cv2.ImWrite("test_img/3-otsu.jpg", otsu);
    }

    //後處理
    //後處理-closing
    static void doClosing()
    {
        //修補小缺口、小斷裂
        System.Console.WriteLine("doClosing");
        System.Console.WriteLine("修補小缺口、小斷裂");
        closing = new Mat();
        Cv2.MorphologyEx(otsu, closing, MorphTypes.Close, kernel);
        Cv2.ImWrite("test_img/4-closing.jpg", closing);
    }

    //特徵擷取
    //特徵擷取-物件輪廓
    static void doFindContours()
    {
        System.Console.WriteLine("doFindContours");
        Cv2.FindContours(
            closing,
            out contours,//輪廓們
            out hierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        //複製一份原圖用來畫圖
        contourImage = original.Clone();

        Cv2.DrawContours(
            contourImage,
            contours,//輪廓們
            -1, // -1 = 全部輪廓
            Scalar.Green,
            2
        );

        Cv2.ImWrite("test_img/5-contours.jpg", contourImage);
    }

    static void CatchMaxContour()
    {
        System.Console.WriteLine("CatchMaxContour");
        System.Console.WriteLine("直接取最大面積，因為圖片只有一顆螺絲所以不需要先過濾面積大小");
        if (contours.Length == 0)//一開始mainContour本來就是null
        {
            return;
        }

        //LINQ語法：
        //countours=所有輪廓
        //OrderByDescending：由大到小排序
        //Cv2.ContourArea：計算面積
        //First：取第一個
        mainContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();//最大輪廓
        //mainRect = Cv2.BoundingRect(mainContour);//最大輪廓的外接矩形

        //畫成圖片
        contourImage = original.Clone();

        Cv2.DrawContours(
            contourImage,
            new[] { mainContour },//最大輪廓
            -1,
            Scalar.Red,
            2);

        Cv2.ImWrite("test_img/6-maxContour.jpg", contourImage);
    }

    static void fullTheHole() {
        //因為物件跟背景的邊界是連續的，所以可以把洞塗滿方便之後找螺帽跟螺牙的邊界
        System.Console.WriteLine("fullTheHole");
        System.Console.WriteLine("把洞塗滿方便之後找螺帽跟螺牙的邊界");
        filledMask = Mat.Zeros(
            closing.Size(),
            MatType.CV_8UC1
        );

        Cv2.DrawContours(
            filledMask,
            new[] { mainContour },//最大輪廓
            -1,
            Scalar.White,
            -1//填滿輪廓內部
        );

        Cv2.ImWrite("test_img/7-filledMask.jpg", filledMask);
    }

    //外接矩形
    static void doBoundingRect() {
        System.Console.WriteLine("doBoundingRect");
        //if (contours.Length == 0)
        //{
        //    System.Console.WriteLine("找不到輪廓");
        //    return;
        //}
        mainRect = Cv2.BoundingRect(mainContour);//最大輪廓的外接矩形
        boundingImage = original.Clone();

        //foreach (var contour in contours)
        //{
            //Rect rect = Cv2.BoundingRect(contour);

            Cv2.Rectangle(
                boundingImage,
                mainRect,
                Scalar.Blue,
                2);
        //}

        Cv2.ImWrite("test_img/8-boundingRect.jpg", boundingImage);
    }

    static void getMinAreaRect() {
        System.Console.WriteLine("getMinAreaRect");
        //Minimum Area Rotated Rectangle（最小外接旋轉矩形）
        //找一個可以旋轉的矩形，而且面積最小，剛好包住這個輪廓。
        //openCV會用最小矩形包覆住物件，並偵測到以下資訊：
        //Center  ← 中心點
        //Size    ← 長、寬
        //Angle   ← 角度
        mainRotatedRect = Cv2.MinAreaRect(mainContour);//mainContour(最大輪廓)才有point[](輪廓)，filledMask是Mat

        //先取得四個角
        Point2f[] points = mainRotatedRect.Points();
      
        //複製一份原圖用來畫圖
        minAreaRectImage = original.Clone();

        //把四個角串聯
        for (int i = 0; i < 4; i++)
        {
            Console.WriteLine("外接矩形的四個角座標="+$"{i}: {points[i]}");
            Cv2.Line(
                minAreaRectImage,
                (Point)points[i],
                (Point)points[(i + 1) % 4],
                Scalar.Blue,
                2);
        }

        Cv2.ImWrite("test_img/9-MinAreaRect.jpg", minAreaRectImage);

    }

    static void doRotated(){
        System.Console.WriteLine("doRotated");
        System.Console.WriteLine("使用最小外接旋轉矩形將物件擺正方便後續分析螺帽跟螺牙的區域界線");
        //這樣所有量測都在同一個座標系中進行，不需要針對不同角度設計不同的演算法。
        rotatedImage = new Mat();
        
        //取出偵測到的angle
        double angle = mainRotatedRect.Angle;

        //EX：Angle = -87.3447=>openCV覺得這個外接矩形順時針歪了 87°
        //        90°
        //           ↑
        //           |
        //180° ------+------0°
        //           |
        //           ↓
        //         -90°
        System.Console.WriteLine($"Angle = {mainRotatedRect.Angle}");
        System.Console.WriteLine($"Width = {mainRotatedRect.Size.Width}");
        System.Console.WriteLine($"Height = {mainRotatedRect.Size.Height}");
        System.Console.WriteLine($"Center = {mainRotatedRect.Center}");
        //因為openCV不知道人類眼中的長跟寬，對他來說兩個矩形即便顛倒也是同個東西
        //為了避免OpenCV交換 Width、Height，因此直接指定規則把旋轉的角度修正
        if (mainRotatedRect.Size.Width < mainRotatedRect.Size.Height)
        {
            angle += 90;
        }

        //建立旋轉矩陣，請openCV幫我算該怎麼旋轉圖片
        Mat rotationMatrix = Cv2.GetRotationMatrix2D(
            mainRotatedRect.Center,
            angle,
            1.0
        );
        angle = -(mainRotatedRect.Angle + 90);

        //用WarpAffine把整張圖旋轉
        Cv2.WarpAffine(
            filledMask,
            rotatedImage,
            rotationMatrix,
            closing.Size()
        );

        Cv2.ImWrite("test_img/10-rotated.jpg", rotatedImage);
    }

    static void getRotatedBoundingRect() {
        System.Console.WriteLine("getRotatedBoundingRect");
        System.Console.WriteLine("旋轉後原先座標已失真，故重新獲取");

        System.Console.WriteLine("重新取得輪廓");

        Cv2.FindContours(
            rotatedImage,//旋轉後的圖
            out rotatedContours,
            out rotatedHierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple
        );

        System.Console.WriteLine("重新取得最大輪廓");
        rotatedMainContour =rotatedContours.OrderByDescending(c => Cv2.ContourArea(c)).First();

        System.Console.WriteLine("重新取得最大輪廓的外接矩形");
        rotatedBoundingRect = Cv2.BoundingRect(rotatedMainContour);

        //畫成圖片
        //轉成彩色圖
        rotatedContourImage = new Mat();

        Cv2.CvtColor(
            rotatedImage,//目標：目前正在分析的rotatedImage
            rotatedContourImage,
            ColorConversionCodes.GRAY2BGR
        );

        Cv2.DrawContours(
            rotatedContourImage,
            new[] { rotatedMainContour },//旋轉後最大輪廓
            -1,
            Scalar.Blue,
            2);

        Cv2.ImWrite("test_img/11-rotatedContour.jpg", rotatedContourImage);
        //System.Console.WriteLine("但是，這是根據最小旋轉外接矩形來判斷怎麼旋轉。不符合人眼中的水平。");
        //System.Console.WriteLine("所以改用FitLine會更符合這種不規則形狀的工件");
    }

    static void doFitLine() {
        System.Console.WriteLine("doFitLine");
        System.Console.WriteLine("找一條代表方向的直線");
        //輸出：
        //一個方向向量(vx, vy)
        //一個在線上的點(x0, y0)
        Line2D line = Cv2.FitLine(
            mainContour,
            DistanceTypes.L2,
            0,
            0.01,
            0.01
        );

        //line包含以下四個東西
        double vx = line.Vx;
        double vy = line.Vy;
        double x1 = line.X1;
        double y1 = line.Y1;

        System.Console.WriteLine("vx="+vx);
        System.Console.WriteLine("vy=" + vy);
        System.Console.WriteLine("x1=" + x1);
        System.Console.WriteLine("y1=" + y1);

        System.Console.WriteLine("計算螺絲的真正主軸角度");
        double angle = Math.Atan2(vy, vx) * 180 / Math.PI;
        System.Console.WriteLine("螺絲的主軸角度="+ angle);

        System.Console.WriteLine("旋轉至真正的水平");
        Point2f center = new Point2f((float)x1, (float)y1);
        Mat rotation = Cv2.GetRotationMatrix2D(
            center,
            angle,//測試後angle反而能擺正而不是-angle
            1.0
        );

        System.Console.WriteLine("先畫個樣本確認這條主軸跟人想的是否一樣");
        Mat debug = original.Clone();

        Point pt1 = new Point(
            x1 - vx * 1000,
            y1 - vy * 1000);

        Point pt2 = new Point(
            x1 + vx * 1000,
            y1 + vy * 1000);

        Cv2.Line(debug, pt1, pt2, Scalar.Red, 2);

        Cv2.ImWrite("test_img/12-1-fitLine.jpg", debug);

        System.Console.WriteLine("開始測試旋轉到水平");
        fitlineRotateResult = new Mat();

        Cv2.WarpAffine(
            filledMask,//被旋轉的圖
            fitlineRotateResult,
            rotation,
            filledMask.Size());
        Cv2.ImWrite("test_img/12-2-fitlineRotate.jpg", fitlineRotateResult);
        //System.Console.WriteLine("主軸雖然有些不夠完美水平，但人為覺得可以容許誤差");
    }

    static void getFitlineRotatedBoundingRect()
    {
        System.Console.WriteLine("getFitlineRotatedBoundingRect");
        System.Console.WriteLine("旋轉後原先座標已失真，故重新獲取");

        System.Console.WriteLine("重新取得輪廓");

        Cv2.FindContours(
            fitlineRotateResult,//旋轉後的圖
            out fitlineRotatedContours,
            out fitlineRotatedHierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple
        );

        System.Console.WriteLine("重新取得最大輪廓");
        fitlineRotatedMainContour = fitlineRotatedContours.OrderByDescending(c => Cv2.ContourArea(c)).First();

        System.Console.WriteLine("重新取得最大輪廓的外接矩形");
        fitlineRotatedBoundingRect = Cv2.BoundingRect(fitlineRotatedMainContour);

        //畫成圖片
        //轉成彩色圖
        fitlineRotatedContourImage = new Mat();

        Cv2.CvtColor(
            fitlineRotateResult,//目標：目前正在分析的fitlineRotateResult
            fitlineRotatedContourImage,
            ColorConversionCodes.GRAY2BGR
        );

        Cv2.DrawContours(
            fitlineRotatedContourImage,
            new[] { fitlineRotatedMainContour },//旋轉後最大輪廓
            -1,
            Scalar.Blue,
            2);

        Cv2.ImWrite("test_img/13-fitlineRotatedContour.jpg", fitlineRotatedContourImage);
        //System.Console.WriteLine("但是，這是根據最小旋轉外接矩形來判斷怎麼旋轉。不符合人眼中的水平。");
        //System.Console.WriteLine("所以改用FitLine會更符合這種不規則形狀的工件");
    }


    static void roiRotatedImage() {
        System.Console.WriteLine("roiRotatedImage");
        System.Console.WriteLine("裁切背景保留輪廓方便掃描，避免把背景跟物件造成的變化當做螺帽和螺牙的高度變化");

        //請從這張 Mat 按照 Rect 的座標切一塊出來。
        //openCV不會知道這兩張圖的關係，是因為我知道rotatedBoundingRect是從rotatedImage得出的
        roiImage = new Mat(
            fitlineRotateResult,//旋轉後的圖(背景)
            fitlineRotatedBoundingRect//旋轉後的外接矩形
        ).Clone(); ;
        Cv2.ImWrite("test_img/14-roi.jpg",roiImage);
    }

    static void scanRotatedImage() {
        System.Console.WriteLine("scanRotatedImage");
        System.Console.WriteLine("沿著X軸從左到右掃描ROI後只剩目標物件的圖片");
        //左上角是(0,0)
        //x →    0 1 2 3 4 5 6 7 8

        //y = 0   0 0 0 0 0 0 0 0 0
        //y = 1   0 0 255 255 255 0 0 0 0
        //y = 2   0 0 255 255 255 0 0 0 0
        //y = 3   0 0 255 255 255 0 0 0 0
        //y = 4   0 0 255 255 255 0 0 0 0

        System.Console.WriteLine("取得每個x軸上有多少白色pixel表示物件");
        List<int> objectPixelAmount = new List<int>();//有多少白色pixel(物件)

        for (int x = 0; x < roiImage.Cols; x++)//決定掃瞄X軸的哪一列
        {
            int count = 0;

            for (int y = 0; y < roiImage.Rows; y++)//固定X軸不動往下掃描
            {
                if (roiImage.At<byte>(y, x) == 255)//取得 (x,y) 這個像素的值。
                    count++;//物件(白色)像素+1
            }
            objectPixelAmount.Add(count);
            System.Console.WriteLine($"x={x}, 白色像素={count}");
        }

        System.Console.WriteLine("紀錄x和(x-1)之間的pixel差異值");
        List<int> differences = new List<int>();//前後差異質

        differences.Add(0);   // x=0 沒有前一個，所以補 0

        for (int i = 1; i < objectPixelAmount.Count; i++)
        {
            differences.Add(
                Math.Abs(objectPixelAmount[i] - objectPixelAmount[i - 1])
            );
        }

        System.Console.WriteLine("整理成CSV檔案");
        using (StreamWriter writer = new StreamWriter("test_img/scanRotatedImageResult.csv"))
        {
            // 標題列
            writer.WriteLine("X,WhitePixel,Difference");

            for (int x = 0; x < objectPixelAmount.Count; x++)
            {
                writer.WriteLine(
                    $"{x},{objectPixelAmount[x]},{differences[x]}"
                );
            }
        }

        System.Console.WriteLine("CSV檔案已輸出");
    }

    static void cutImage() {
        System.Console.WriteLine("cutIamge");
        System.Console.WriteLine("人為根據報表的資訊推測螺帽跟螺牙的分界點並做測試是否如預期");

        int boundary =329; // xX軸= 329
        System.Console.WriteLine("手動填寫測試界線，X軸="+ boundary);

        Rect nutROI = new Rect(
            boundary,
            0,
            roiImage.Cols - boundary,
            roiImage.Rows
        );

        nut = new Mat(roiImage, nutROI);

        Rect threadROI = new Rect(
            0,
            0,
            boundary,
            roiImage.Rows
        );

        thread = new Mat(roiImage, threadROI);

        Cv2.ImWrite("test_img/15-1-nut.jpg", nut);
        Cv2.ImWrite("test_img/15-2-thread.jpg", thread);
    }

    static void measuringNut() {
        System.Console.WriteLine("measuringNut");

        System.Console.WriteLine("取得螺帽的輪廓");
        
        Point[][] nutContours;//螺帽的輪廓
        HierarchyIndex[] nutHierarchy;

        Cv2.FindContours(
            nut,//切下來的螺帽圖
            out nutContours,
            out nutHierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);
        if (nutContours.Length==0)
        {
            System.Console.WriteLine("nutMainContour is null");
        }

        nutMainContour = nutContours[0];//我很篤定只會有一個輪廓，所以不需要去找最大輪廓

        //畫成圖片
        //轉成彩色
        nutContourImage = new Mat();

        Cv2.CvtColor(
          nut,//目標：目前正在分析的nut.jpg
          nutContourImage,
          ColorConversionCodes.GRAY2BGR
        );

        Cv2.DrawContours(
             nutContourImage,
             new[] { nutMainContour },//最大輪廓
             -1,
             Scalar.Red,
             2);

        Cv2.ImWrite("test_img/16-1-nutContourImage.jpg", nutContourImage);

        System.Console.WriteLine("取得螺帽輪廓的外接矩形");
        nutMainRect = Cv2.BoundingRect(nutMainContour);//外接矩形

        //畫成圖片
        nutMainRectImage = nutContourImage.Clone();

        Cv2.Rectangle(
                nutMainRectImage,
                nutMainRect,
                Scalar.Blue,
                2);

        Cv2.ImWrite("test_img/16-2-nutMainRect.jpg", nutMainRectImage);
    }

    static void measuringThread() {
        System.Console.WriteLine("measuringThread");

        System.Console.WriteLine("取得螺牙的輪廓");

        Point[][] threadContours;//螺帽的輪廓
        HierarchyIndex[] threadHierarchy;

        Cv2.FindContours(
            thread,//切下來的螺牙圖
            out threadContours,
            out threadHierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);
        if (threadContours.Length == 0)
        {
            System.Console.WriteLine("threadMainContour is null");
        }

        threadMainContour = threadContours[0];//我很篤定只會有一個輪廓，所以不需要去找最大輪廓

        //畫成圖片
        //轉成彩色
        threadContourImage = new Mat();

        Cv2.CvtColor(
          thread,//目標：目前正在分析的nut.jpg
          threadContourImage,
          ColorConversionCodes.GRAY2BGR
        );

        Cv2.DrawContours(
             threadContourImage,
             new[] { threadMainContour },//最大輪廓
             -1,
             Scalar.Red,
             2);

        Cv2.ImWrite("test_img/17-1-nutContourImage.jpg", threadContourImage);

        System.Console.WriteLine("取得螺牙輪廓的外接矩形");
        threadMainRect = Cv2.BoundingRect(threadMainContour);//外接矩形

        //畫成圖片
        threadMainRectImage = threadContourImage.Clone();

        Cv2.Rectangle(
                threadMainRectImage,
                threadMainRect,
                Scalar.Blue,
                2);

        Cv2.ImWrite("test_img/17-2-threadMainRect.jpg", threadMainRectImage);
    }

    static void getObjectInfo() {
        System.Console.WriteLine("getObjectInfo");

        System.Console.WriteLine("<---螺帽測量資訊--->");
        double nutWidth = nutMainRect.Width;
        double nutHeight = nutMainRect.Height;

        double nutArea = Cv2.ContourArea(nutMainContour);

        double nutPerimeter = Cv2.ArcLength(nutMainContour, true);
        System.Console.WriteLine($"螺帽寬      : {nutWidth}");
        System.Console.WriteLine($"螺帽長      : {nutHeight}");
        System.Console.WriteLine($"螺帽面積    : {nutArea}");
        System.Console.WriteLine($"螺帽周長    : {nutPerimeter}");
        System.Console.WriteLine("<---螺帽測量資訊--->");

        System.Console.WriteLine("<---螺牙測量資訊--->");
        double threadWidth = threadMainRect.Width;
        double threadHeight = threadMainRect.Height;

        double threadArea = Cv2.ContourArea(threadMainContour);

        double threadPerimeter = Cv2.ArcLength(threadMainContour, true);
        System.Console.WriteLine($"螺牙寬      : {threadWidth}");
        System.Console.WriteLine($"螺牙長      : {threadHeight}");
        System.Console.WriteLine($"螺牙面積    : {threadArea}");
        System.Console.WriteLine($"螺牙周長    : {threadPerimeter}");
        System.Console.WriteLine("<---螺牙測量資訊--->");

        System.Console.WriteLine("存取正確樣品的資訊供比對");

        correctSample.headWidth = nutWidth;
        correctSample.headHeight = nutHeight;
        correctSample.headArea = nutArea;
        correctSample.headPerimeter = nutPerimeter;
        correctSample.threadWidth = threadWidth;
        correctSample.threadLength = threadHeight;
        correctSample.threadArea = threadArea;
        correctSample.threadPerimeter = threadPerimeter;
    }

    static void createBadSample()
    {
        System.Console.WriteLine("人為的製作缺陷品");

        System.Console.WriteLine("螺牙太短");
        Cv2.Rectangle(
            thread,
            new Rect(0, 0, 20, thread.Rows),
            Scalar.Black,
            -1
        );

        Cv2.ImWrite("test_img/thread_short.jpg", thread);
    }

    static void compareCenter(Mat doCompareImage) {
        //doCompareImage是缺陷品，Ex：thread_short.jpg
        System.Console.WriteLine("取得螺牙的輪廓");

        Point[][] compareImageContours;//螺牙的輪廓
        HierarchyIndex[] compareImageHierarchy;

        System.Console.WriteLine(doCompareImage.Type());
        System.Console.WriteLine(doCompareImage.Channels());

        Cv2.FindContours(
            doCompareImage,//比較目標
            out compareImageContours,
            out compareImageHierarchy,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple
        );

        if (compareImageContours.Length == 0)
        {
            System.Console.WriteLine("compareImageContours is null");
        }

        System.Console.WriteLine($"輪廓數量：{compareImageContours.Length}");

        foreach (var c in compareImageContours)
        {
            System.Console.WriteLine($"Point數：{c.Length}");
        }

        //compareImageMainContour = compareImageContours[0];//我很篤定只會有一個輪廓，所以不需要去找最大輪廓
        System.Console.WriteLine("資料格式轉換時會產生雜訊，所以還是保留找最大輪廓");
        compareImageMainContour =compareImageContours.OrderByDescending(c => Cv2.ContourArea(c)).First();

        //畫成圖片
        //轉成彩色
        compareImageContourImage = new Mat();

        Cv2.CvtColor(
          doCompareImage,//目標：目前正在分析的doCompareImage
          compareImageContourImage,
          ColorConversionCodes.GRAY2BGR
        );

        Cv2.DrawContours(
             compareImageContourImage,
             new[] { compareImageMainContour },//最大輪廓
             -1,
             Scalar.Red,
             2);

        Cv2.ImWrite("test_img/compare-1-compareImageContourImage.jpg", compareImageContourImage);

        System.Console.WriteLine("取得螺牙輪廓的外接矩形");
        compareImageMainRect = Cv2.BoundingRect(compareImageMainContour);//外接矩形

        //畫成圖片
        compareImageMainRectImage = compareImageContourImage.Clone();

        Cv2.Rectangle(
                compareImageMainRectImage,
                compareImageMainRect,
                Scalar.Blue,
                2);

        Cv2.ImWrite("test_img/compare-2-compareImageMainRectImage.jpg", compareImageMainRectImage);

        
        System.Console.WriteLine("<---螺牙測量資訊--->");
        double threadWidth = compareImageMainRect.Width;
        double threadHeight = compareImageMainRect.Height;

        double threadArea = Cv2.ContourArea(compareImageMainContour);

        double threadPerimeter = Cv2.ArcLength(compareImageMainContour, true);
        System.Console.WriteLine($"螺牙寬      : {threadWidth}");
        System.Console.WriteLine($"螺牙長      : {threadHeight}");
        System.Console.WriteLine($"螺牙面積    : {threadArea}");
        System.Console.WriteLine($"螺牙周長    : {threadPerimeter}");
        System.Console.WriteLine("<---螺牙測量資訊--->");

        System.Console.WriteLine("假設只有螺牙的長度出現缺陷");
        System.Console.WriteLine("測試品的資訊與正確樣品比對");

        List<string> compareResult = new List<string>();

        if (correctSample.threadLength< threadWidth)
        {
            compareResult.Add("螺牙太短");
        }

        if (correctSample.threadLength> threadWidth)
        {
            compareResult.Add("螺牙太長");
        }

        System.Console.WriteLine("<---比對結果--->");
        if (compareResult.Count == 0)
        {
            System.Console.WriteLine("PASS");
        }
        else
        {
            System.Console.WriteLine("FAIL");

            foreach (var reason in compareResult)
            {
                System.Console.WriteLine(reason);
            }
        }
        System.Console.WriteLine("<---比對結果--->");
    }

}//end by class program