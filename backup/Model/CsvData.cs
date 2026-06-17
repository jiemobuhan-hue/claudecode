using DevExpress.ClipboardSource.SpreadsheetML;
using HslCommunication;
using RinKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Model
{
    public class CsvData
    {
        public string 电芯码 { get; set; }
        public string 进站时间 { get; set; }
        
        public string 出站结果 { get; set; }
        public string 出站时间 { get; set; }

        public CsvData(CellData data)
        {
            电芯码 = data.电芯码;
            出站结果 = data.出站结果;
            出站时间 = data.出站时间;
        }

        public void Save()
        {
            if(DateTime.TryParse(进站时间, out DateTime time))
            {
                CsvHelper.Save(this, $"{time.Year}-{time.Month}-{time.Day}.csv");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CsvDataService  —— 所有 CSV 读写操作
    //
    //  数据文件:
    //   inspection_records.csv  —— 每次检测的汇总记录（一产品一行）
    //   image_records.csv       —— 每张检测图片的详细信息（一图一行）
    //
    //  生产项目建议换成 CsvHelper NuGet 包以支持转义/引号等边界情况。
    //  当前实现为轻量级手写解析，满足工厂单工位单用户场景。
    // ═══════════════════════════════════════════════════════════════════
    public class CsvDataService
    {
        private readonly string _dataDir;
        private readonly string _recordsFile;
        private readonly string _imagesFile;

        // CSV 列定义（按索引访问，避免 header 映射出错）
        // inspection_records.csv
        private const int R_RecordId = 0, R_CellCode = 1, R_DateTime = 2, R_LineId = 3,
            R_StationId = 4, R_OverallResult = 5, R_NgTypes = 6, R_ProcessMs = 7;

        // image_records.csv
        private const int I_ImageId = 0, I_RecordId = 1, I_CellCode = 2, I_StationId = 3,
            I_AngleName = 4, I_ImagePath = 5, I_VisionResult = 6, I_VisionScore = 7,
            I_NgType = 8, I_DefectBbox = 9, I_ManualResult = 10, I_ManualTime = 11,
            I_ManualUser = 12, I_ManualComment = 13, I_IsManualReviewed = 14;

        private static readonly JsonSerializerOptions JsonOpts =
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public CsvDataService(string dataDir)
        {
            _dataDir = dataDir;
            _recordsFile = Path.Combine(dataDir, "inspection_records.csv");
            _imagesFile = Path.Combine(dataDir, "image_records.csv");
        }

        // ═══════════════════════════════════════════════════════════
        //  看板数据
        // ═══════════════════════════════════════════════════════════
        public string GetDashboardSummaryJson(DateTime date)
        {
            var records = ReadRecords()
                .Where(r => DateTime.TryParse(r[R_DateTime], out var dt) && dt.Date == date.Date)
                .ToList();

            int total = records.Count;
            int ok = records.Count(r => r[R_OverallResult] == "OK");
            int ng = total - ok;
            double yieldRate = total == 0 ? 0 : Math.Round(ok * 100.0 / total, 2);

            // NG 类型统计
            var ngTypes = records
                .Where(r => r[R_OverallResult] == "NG")
                .SelectMany(r => r[R_NgTypes].Split('|'))
                .GroupBy(t => t)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            // 时段产量（按小时统计）
            var hourly = Enumerable.Range(0, 24).Select(h =>
            {
                var inHour = records.Where(r =>
                    DateTime.TryParse(r[R_DateTime], out var dt) && dt.Hour == h).ToList();
                return new
                {
                    hour = $"{h:D2}:00",
                    ok = inHour.Count(x => x[R_OverallResult] == "OK"),
                    ng = inHour.Count(x => x[R_OverallResult] == "NG")
                };
            }).Where(x => x.ok + x.ng > 0).ToList();

            // 最近 20 条记录
            var recent = records
                .OrderByDescending(r => r[R_DateTime])
                .Take(20)
                .Select(r => new
                {
                    recordId = r[R_RecordId],
                    cellCode = r[R_CellCode],
                    dateTime = r[R_DateTime],
                    stationId = r[R_StationId],
                    overallResult = r[R_OverallResult],
                    ngTypes = r[R_NgTypes]
                })
                .ToList();

            return JsonSerializer.Serialize(new { total, ok, ng, yieldRate, ngTypes, hourly, recent }, JsonOpts);
        }

        public string GetLiveStatusJson()
        {
            var last = ReadRecords()
                .OrderByDescending(r => r[R_DateTime])
                .FirstOrDefault();

            if (last == null)
                return JsonSerializer.Serialize(new { lastResult = "NONE", lastCellCode = "-", lastTime = "-" });

            // 今日统计
            var today = ReadRecords()
                .Where(r => DateTime.TryParse(r[R_DateTime], out var dt) && dt.Date == DateTime.Today)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                lastResult = last[R_OverallResult],
                lastCellCode = last[R_CellCode],
                lastTime = last[R_DateTime],
                todayTotal = today.Count,
                todayOk = today.Count(r => r[R_OverallResult] == "OK"),
                todayNg = today.Count(r => r[R_OverallResult] == "NG")
            }, JsonOpts);
        }

        // ═══════════════════════════════════════════════════════════
        //  复检页搜索
        // ═══════════════════════════════════════════════════════════
        public string SearchRecordsJson(string cellCode, string dateFrom, string dateTo,
                                         string resultFilter, int pageIndex, int pageSize)
        {
            pageSize = Math.Max(1, Math.Min(pageSize, 200));
            pageIndex = Math.Max(0, pageIndex);

            var query = ReadRecords().AsEnumerable();

            if (!string.IsNullOrEmpty(cellCode))
                query = query.Where(r => r[R_CellCode].Contains(cellCode));

            if (DateTime.TryParse(dateFrom, out var from))
                query = query.Where(r => DateTime.TryParse(r[R_DateTime], out var dt) && dt >= from);

            if (DateTime.TryParse(dateTo, out var to))
                query = query.Where(r => DateTime.TryParse(r[R_DateTime], out var dt) && dt <= to.AddDays(1));

            if (!string.IsNullOrEmpty(resultFilter) && resultFilter != "ALL")
                query = query.Where(r => r[R_OverallResult] == resultFilter);

            var sorted = query.OrderByDescending(r => r[R_DateTime]).ToList();
            int total = sorted.Count;
            var page = sorted.Skip(pageIndex * pageSize).Take(pageSize)
                .Select(r => new
                {
                    recordId = r[R_RecordId],
                    cellCode = r[R_CellCode],
                    dateTime = r[R_DateTime],
                    lineId = r[R_LineId],
                    stationId = r[R_StationId],
                    overallResult = r[R_OverallResult],
                    ngTypes = r[R_NgTypes],
                    processMs = r[R_ProcessMs]
                })
                .ToList();

            return JsonSerializer.Serialize(new { total, pageIndex, pageSize, records = page }, JsonOpts);
        }

        // ═══════════════════════════════════════════════════════════
        //  获取产品图片
        // ═══════════════════════════════════════════════════════════
        public string GetProductImagesJson(string cellCode)
        {
            var images = ReadImages()
                .Where(r => r[I_CellCode] == cellCode)
                .Select(r => new
                {
                    imageId = r[I_ImageId],
                    recordId = r[I_RecordId],
                    stationId = r[I_StationId],
                    angleName = r[I_AngleName],
                    // 使用虚拟主机名 localimg: https://localimg/BC001/front.jpg
                    imagePath = $"https://localimg/{r[I_ImagePath]}",
                    visionResult = r[I_VisionResult],
                    visionScore = r[I_VisionScore],
                    ngType = r[I_NgType],
                    defectBbox = r[I_DefectBbox],
                    isManualReviewed = r[I_IsManualReviewed] == "1",
                    manualResult = r[I_ManualResult],
                    manualTime = r[I_ManualTime],
                    manualUser = r[I_ManualUser],
                    manualComment = r[I_ManualComment]
                })
                .ToList();

            return JsonSerializer.Serialize(new { cellCode, images }, JsonOpts);
        }

        // ═══════════════════════════════════════════════════════════
        //  保存人工复检结果（线程安全：单用户场景直接文件锁）
        // ═══════════════════════════════════════════════════════════
        public bool SaveManualResult(string imageId, string result, string user, string comment)
        {
            var rows = ReadImages();
            bool found = false;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i][I_ImageId] != imageId) continue;
                rows[i][I_ManualResult] = result;
                rows[i][I_ManualTime] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                rows[i][I_ManualUser] = user;
                rows[i][I_ManualComment] = comment.Replace(",", "，"); // 防止 CSV 破坏
                rows[i][I_IsManualReviewed] = "1";
                found = true;
                break;
            }

            if (!found) return false;

            WriteImages(rows);
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  CSV 底层读写
        // ═══════════════════════════════════════════════════════════
        private List<string[]> ReadRecords()
        {
            if (!File.Exists(_recordsFile)) return new List<string[]>();
            return File.ReadAllLines(_recordsFile, Encoding.UTF8)
                .Skip(1)  // 跳过 header
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(','))
                .Where(f => f.Length > R_ProcessMs)
                .ToList();
        }

        private List<string[]> ReadImages()
        {
            if (!File.Exists(_imagesFile)) return new List<string[]>();
            return File.ReadAllLines(_imagesFile, Encoding.UTF8)
                .Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(','))
                .Where(f => f.Length > I_IsManualReviewed)
                .ToList();
        }

        private void WriteImages(List<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ImageId,RecordId,CellCode,StationId,AngleName,ImagePath," +
                          "VisionResult,VisionScore,NgType,DefectBbox," +
                          "ManualResult,ManualTime,ManualUser,ManualComment,IsManualReviewed");
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row));
            File.WriteAllText(_imagesFile, sb.ToString(), Encoding.UTF8);
        }

        // ═══════════════════════════════════════════════════════════
        //  生成示例数据（首次运行）
        // ═══════════════════════════════════════════════════════════
        public void EnsureSampleData()
        {
            if (File.Exists(_recordsFile) && File.Exists(_imagesFile)) return;

            var rnd = new Random(42);
            var ngTypePool = new[] { "SCRATCH", "DENT", "DIRTY", "EDGE_DEFECT", "COLOR_DIFF" };
            var stations = new[] { "S01", "S02", "S03" };
            var angles = new[] { "正面", "背面", "左侧", "右侧", "顶部", "底部" };

            var recordSb = new StringBuilder();
            recordSb.AppendLine("RecordId,CellCode,DateTime,LineId,StationId,OverallResult,NgTypes,ProcessTimeMs");
            var imageSb = new StringBuilder();
            imageSb.AppendLine("ImageId,RecordId,CellCode,StationId,AngleName,ImagePath," +
                               "VisionResult,VisionScore,NgType,DefectBbox," +
                               "ManualResult,ManualTime,ManualUser,ManualComment,IsManualReviewed");

            int imageIdCounter = 1;
            var baseTime = DateTime.Today.AddHours(-10);

            for (int i = 1; i <= 300; i++)
            {
                var cellCode = $"BC{DateTime.Today:yyyyMMdd}{i:D4}";
                var dt = baseTime.AddSeconds(i * 18 + rnd.Next(5));
                var station = stations[rnd.Next(stations.Length)];
                bool isNg = rnd.NextDouble() < 0.08; // 8% NG率
                var ngTypesStr = "";
                if (isNg)
                {
                    int count = rnd.Next(1, 3);
                    ngTypesStr = string.Join("|", ngTypePool.OrderBy(_ => rnd.Next()).Take(count));
                }

                recordSb.AppendLine($"{i},{cellCode},{dt:yyyy-MM-dd HH:mm:ss},Line1,{station}," +
                                    $"{(isNg ? "NG" : "OK")},{ngTypesStr},{rnd.Next(800, 1500)}");

                // 每个产品 6 张图
                foreach (var angle in angles)
                {
                    bool imgNg = isNg && rnd.NextDouble() < 0.4;
                    var ngType = imgNg ? ngTypePool[rnd.Next(ngTypePool.Length)] : "";
                    var bbox = imgNg ? $"[{rnd.Next(10, 200)};{rnd.Next(10, 200)};{rnd.Next(20, 100)};{rnd.Next(20, 100)}]" : "";
                    double score = imgNg ? Math.Round(0.7 + rnd.NextDouble() * 0.2, 3)
                                        : Math.Round(0.9 + rnd.NextDouble() * 0.1, 3);

                    // 图片路径指向 localimg 虚拟主机（实际生产中是真实图片）
                    var imgPath = $"{cellCode}/{angle.Replace("/", "_")}.jpg";

                    imageSb.AppendLine($"{imageIdCounter++},{i},{cellCode},{station},{angle},{imgPath}," +
                                       $"{(imgNg ? "NG" : "OK")},{score},{ngType},{bbox}," +
                                       $",,,, 0");
                }
            }

            File.WriteAllText(_recordsFile, recordSb.ToString(), Encoding.UTF8);
            File.WriteAllText(_imagesFile, imageSb.ToString(), Encoding.UTF8);
        }
    }
}
//public class CsvData
//{
//    public string 电芯码 { get; set; }
//    public string 进站时间 { get; set; }
//    public float 一注前称重 { get; set; }
//    public float 一注后称重 { get; set; }
//    public float 前称重重量 { get; set; }
//    public float 后称重重量 { get; set; }
//    public float 化成失液量 { get; set; }
//    public float 目标注液量 { get; set; }
//    public float 实际注液量 { get; set; }
//    public float 目标保有量 { get; set; }
//    public float 实际保有量 { get; set; }
//    public float 保压真空目标值 { get; set; }
//    public float 抽真空时间 { get; set; }
//    public float 保压时间 { get; set; }
//    public float 注液时间 { get; set; }
//    public float 保压前真空 { get; set; }
//    public float 保压后真空 { get; set; }
//    public float 注液正压目标值 { get; set; }
//    public float 正压时间 { get; set; }
//    public float 胶钉高度 { get; set; }
//    public string 入站结果 { get; set; }
//    public string 拔钉结果 { get; set; }
//    public string 前称重结果 { get; set; }
//    public string 真空检测结果 { get; set; }
//    public string 后称重结果 { get; set; }
//    public string 胶钉检测结果 { get; set; }
//    public string 注液工位 { get; set; }
//    public string 前称重工位 { get; set; }
//    public string 后称重工位 { get; set; }
//    public string 出站结果 { get; set; }
//    public string 出站时间 { get; set; }

//    public CsvData(CellData data)
//    {
//        电芯码 = data.电芯码;
//        //进站时间 = data.进站时间;
//        //一注前称重 = data.一注前称重;
//        //一注后称重 = data.一注后称重;
//        //前称重重量 = data.前称重重量;
//        //后称重重量 = data.后称重重量;
//        //化成失液量 = data.化成失液量;
//        //目标注液量 = data.目标注液量;
//        //实际注液量 = data.实际注液量;
//        //目标保有量 = data.目标保有量;
//        //实际保有量 = data.实际保有量;
//        //保压真空目标值 = data.保压真空目标值;
//        //抽真空时间 = data.抽真空时间;
//        //保压时间 = data.保压时间;
//        //注液时间 = data.注液时间;
//        //保压前真空 = data.保压前真空;
//        //保压后真空 = data.保压后真空;
//        //注液正压目标值 = data.注液正压目标值;
//        //正压时间 = data.正压时间;
//        //胶钉高度 = data.胶钉高度;
//        //入站结果 = data.入站结果;
//        //拔钉结果 = data.拔钉结果;
//        //前称重结果 = data.前称重结果;
//        //真空检测结果 = data.真空检测结果;
//        //后称重结果 = data.后称重结果;
//        //胶钉检测结果 = data.胶钉检测结果;
//        //注液工位 = data.注液工位;
//        //前称重工位 = data.前称重工位;
//        //后称重工位 = data.后称重工位;
//        出站结果 = data.出站结果;
//        出站时间 = data.出站时间;
//    }

//    public void Save()
//    {
//        if (DateTime.TryParse(进站时间, out DateTime time))
//        {
//            CsvHelper.Save(this, $"{time.Year}-{time.Month}-{time.Day}.csv");
//        }
//    }
//}