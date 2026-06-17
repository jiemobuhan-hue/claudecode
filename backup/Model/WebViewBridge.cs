using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model
{
    // ═══════════════════════════════════════════════════════════════════
    //  WebViewBridge
    //
    //  关键要求:
    //   ① [ComVisible(true)] —— 必须，让 WebView2 可以通过 COM 调用
    //   ② [ClassInterface(AutoDual)] —— 自动生成双接口，支持 JS 按名调用方法
    //   ③ 方法必须是 public，且参数/返回值为 COM 兼容类型（string/int/bool等）
    //
    //  JS 调用方式（异步 Promise）:
    //   const result = await window.chrome.webview.hostObjects.bridge.GetDashboard("2024-01-15");
    //   const obj    = JSON.parse(result);  // Bridge 返回 JSON 字符串
    //
    //  注意: hostObjects 调用在 JS 侧是异步 Promise，即使 C# 方法本身是同步的。
    //        如需批量同步调用，可用 hostObjects.sync.bridge.Method()（性能较差）。
    // ═══════════════════════════════════════════════════════════════════
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class WebViewBridge
    {
        private readonly CsvDataService _csv;
        private static readonly JsonSerializerOptions JsonOpts =
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WebViewBridge(CsvDataService csv) => _csv = csv;

        // ──────────────────────────────────────────────────────────────
        //  [看板页] 获取今日 / 指定日期看板汇总数据
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// 返回 JSON: { total, ok, ng, yieldRate, ngTypes:[{name,count}], hourly:[{hour,ok,ng}] }
        /// </summary>
        public string GetDashboard(string dateStr)
        {
            try
            {
                var date = string.IsNullOrWhiteSpace(dateStr)
                    ? DateTime.Today
                    : DateTime.Parse(dateStr);
                var json = _csv.GetDashboardSummaryJson(date);
                return json;
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// 获取实时产线状态（最近一条记录）
        /// 返回 JSON: { lastResult, lastCellCode, lastTime, todayTotal, todayOk, todayNg }
        /// </summary>
        public string GetLiveStatus()
        {
            try { return _csv.GetLiveStatusJson(); }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────
        //  [复检页] 搜索产品记录
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// 按电芯码/日期范围/结果搜索。
        /// 返回 JSON: { records: [{recordId,cellCode,dateTime,stationId,overallResult,ngTypes}] }
        /// </summary>
        public string SearchRecords(string cellCode, string dateFrom, string dateTo,
                                     string resultFilter, int pageIndex, int pageSize)
        {
            try
            {
                return _csv.SearchRecordsJson(
                    cellCode?.Trim(), dateFrom, dateTo, resultFilter, pageIndex, pageSize);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────
        //  [复检页] 获取某电芯码的全部图片信息
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// 返回 JSON: { cellCode, images: [{imageId,stationId,angleName,imagePath,
        ///                  visionResult,visionScore,ngType,defectBbox,
        ///                  isManualReviewed,manualResult,manualTime,manualUser,manualComment}] }
        /// </summary>
        public string GetProductImages(string cellCode)
        {
            try { return _csv.GetProductImagesJson(cellCode); }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────
        //  [复检页] 保存人工复检结果
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// 保存单张图片的人工复检判断。
        /// imageId: 图片记录 ID
        /// result:  "OK" | "NG"
        /// user:    操作员姓名
        /// comment: 备注
        /// 返回 JSON: { success: true/false, message: "" }
        /// </summary>
        public string SaveManualResult(string imageId, string result, string user, string comment)
        {
            try
            {
                bool ok = _csv.SaveManualResult(imageId, result, user, comment);
                return JsonSerializer.Serialize(new { success = ok, message = ok ? "已保存" : "保存失败" });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>
        /// 批量保存：一次提交整个产品所有图片的复检结果
        /// jsonPayload: [{imageId, result, user, comment}, ...]
        /// </summary>
        public string SaveManualResultBatch(string jsonPayload)
        {
            try
            {
                var items = JsonSerializer.Deserialize<ManualResultItem[]>(jsonPayload, JsonOpts);
                if (items == null) return Error("无效数据");
                int saved = 0;
                foreach (var item in items)
                    if (_csv.SaveManualResult(item.ImageId, item.Result, item.User, item.Comment))
                        saved++;
                return JsonSerializer.Serialize(new { success = true, saved, total = items.Length });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────
        //  辅助
        // ──────────────────────────────────────────────────────────────
        private static string Error(string msg) =>
            JsonSerializer.Serialize(new { error = true, message = msg });
    }

    // 批量复检的 payload 模型
    public class ManualResultItem
    {
        public string ImageId { get; set; } = "";
        public string Result { get; set; } = "";
        public string User { get; set; } = "";
        public string Comment { get; set; } = "";
    } 
    // ── DTO：与 JS window.updateDashboard 参数结构一一对应 ───────
    public class DashboardDto
    {
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Ng { get; set; }
        public double YieldRate { get; set; }
        public List<NgTypeDto> NgTypes { get; set; }
        public List<HourlyDto> Hourly { get; set; }
        public List<RecentDto> Recent { get; set; }
    }
    public class NgTypeDto { public string Name { get; set; } public int Count { get; set; } }
    public class HourlyDto { public string Hour { get; set; } public int Ok { get; set; } public int Ng { get; set; } }
    public class RecentDto
    {
        public string CellCode { get; set; }
        public string DateTime { get; set; }
        public string StationId { get; set; }
        public string OverallResult { get; set; }
        public string NgTypes { get; set; }
        public int ProcessMs { get; set; }
    }
    public class LiveStatusDto
    {
        public string LastResult { get; set; }
        public string LastCellCode { get; set; }
        public string LastTime { get; set; }
        public int TodayOk { get; set; }
        public int TodayNg { get; set; }
        public int TodayTotal { get; set; }
    }
}
