# 三层缺陷复判系统 — 设计规格

Status: approved
Date: 2026-06-09

## 一、目标

将 `UC_InspectionView` 从单层图片复检改造为三层递进式缺陷复判系统：

1. **第一层（大面入口）**: 卡片网格上叠加缺陷统计标签，点击进入缺陷地图
2. **第二层（缺陷地图）**: 大图 + Canvas 缺陷矩形标注 + 右侧缺陷列表，点击缺陷进入详情
3. **第三层（缺陷详情复判）**: 缺陷裁剪图 + 复判表单，提交后刷新前两层

保留搜索、分页、记录选择等基础功能，仅改造图片展示与复检交互。

## 二、重构策略

**方案 B：提取子控件**

- 第三层 `DefectReviewControl` — 独立 UserControl
- 第二层在 GridOverlay 内部重构（不提取独立控件，直接在 XAML 中改为左右分栏布局）
- `UC_InspectionView` 做编排

## 三、数据模型

### 3.1 DefectRegion（新增）

```csharp
public class DefectRegion
{
    public string DefectId { get; set; }          // GUID，唯一标识
    public string DefectType { get; set; }         // 划痕 / 凹坑 / 异物 / 气泡 / 其他
    public double Confidence { get; set; }         // 0.0 ~ 1.0
    public double X { get; set; }                  // 归一化坐标 0.0~1.0
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public ReviewStatus Status { get; set; }       // Unreviewed / OK / NG
    public string ReviewUser { get; set; }
    public string ReviewComment { get; set; }
    public DateTime? ReviewTime { get; set; }
}

public enum ReviewStatus { Unreviewed, OK, NG }
```

- 坐标使用 **归一化值 0.0~1.0**，与图片原始分辨率解耦
- DefectId 使用 GUID 字符串，后续对接 SQL Server 时映射实际字段

### 3.2 ImageRecord 修改

```csharp
// 新增属性
public List<DefectRegion> Defects { get; set; } = new List<DefectRegion>();

// 默认数据生成（视觉坐标未就绪前的占位方案）
public static List<DefectRegion> GenerateDefaultDefects()
{
    // 为每张图片生成 1-3 个默认缺陷，坐标固定在中央区域
    // 类型随机: 划痕/凹坑/异物
    // 后续替换为 BlueFilmDetectionRepository 查询
}
```

### 3.3 SaveReviewArgs 修改

```csharp
// 新增 DefectId 属性，null 表示整图复检（向后兼容）
public string DefectId { get; set; }
```

### 3.4 缺陷类型颜色映射

| 类型 | 颜色 | 色值 |
|------|------|------|
| 划痕 | 红色 | #EF4444 |
| 凹坑 | 橙色 | #F97316 |
| 异物 | 黄色 | #EAB308 |
| 气泡 | 蓝色 | #3B82F6 |
| 其他 | 紫色 | #8B5CF6 |

## 四、坐标转换

### 4.1 归一化坐标 → Canvas 像素坐标

```
scale = min(displayW / originalW, displayH / originalH)
offsetX = (displayW - originalW * scale) / 2
offsetY = (displayH - originalH * scale) / 2

rectX = offsetX + defect.X * originalW * scale
rectY = offsetY + defect.Y * originalH * scale
rectW = defect.Width  * originalW * scale
rectH = defect.Height * originalH * scale
```

- `originalW/H` 从 `BitmapImage.PixelWidth/PixelHeight` 获取
- `displayW/H` 从 `Image.ActualWidth/ActualHeight` 获取
- DefectCanvas 尺寸与 Image 控件一致，矩形直接按 display 坐标放置
- 在 `Image.SizeChanged` 和 `SvZoom.ScrollChanged` 时重绘

### 4.2 归一化坐标 → 原始像素坐标（第三层裁剪用）

```
pixelX = defect.X * bitmap.PixelWidth
pixelY = defect.Y * bitmap.PixelHeight
pixelW = defect.Width  * bitmap.PixelWidth
pixelH = defect.Height * bitmap.PixelHeight

// 扩展 20% padding
cropX = max(0, pixelX - pixelW * 0.1)
cropY = max(0, pixelY - pixelH * 0.1)
cropW = min(pixelW * 1.2, bitmap.PixelWidth - cropX)
cropH = min(pixelH * 1.2, bitmap.PixelHeight - cropY)
```

## 五、各层设计

### 5.1 第一层：卡片网格

**改动文件**: `UC_InspectionView.xaml.cs`

- `BuildPlaceholderCard()`: 在卡片底部增加缺陷统计 WrapPanel，从 `img.Defects.GroupBy(d => d.DefectType)` 生成彩色标签
- `ReplacePlaceholderImage()`: 不变
- 卡片点击: `OpenZoom(index)` → `OpenDefectMap(index)`
- `RefreshCardFooter()`: 从空方法改为更新复判状态图标 + 缺陷统计

### 5.2 第二层：缺陷地图（GridOverlay 重构）

**改动文件**: `UC_InspectionView.xaml`, `UC_InspectionView.xaml.cs`

**XAML 布局**（GridOverlay 内部）:

```
GridOverlay (全屏遮罩 ZIndex=100)
├── 关闭按钮（复用）
├── 内容区 Grid（左右分栏）
│   ├── 左列 (3*)
│   │   ├── SvZoom (ScrollViewer) + ImgZoom (Image)
│   │   ├── DefectCanvas (Canvas, 覆盖在 ImgZoom 上方, 同尺寸)
│   │   └── 小地图区域（CanvasMinimap + ImgMinimap + RectViewport）— 不变
│   └── 右列 (2*)
│       ├── 切换按钮（上一张/下一张，复用）
│       ├── 缺陷列表 (ItemsControl)
│       └── DefectDetailContainer (ContentControl, 第三层容器)
```

**代码逻辑**:

- `OpenDefectMap(ImageRecord img)`: 显示 GridOverlay → `ThumbnailCache.GetOrCreate(path, 2560)` 加载大图 → 调用 `DrawDefectRects(img)` → 绑定缺陷列表
- `DrawDefectRects(ImageRecord img)`: 清空 DefectCanvas → 遍历 `img.Defects` → 按坐标转换公式计算显示坐标 → 创建半透明 Rectangle + 颜色按类型 → 添加 MouseDown 事件 → 添加到 DefectCanvas
- 缺陷列表点击: 高亮对应 Canvas 矩形 → 在 DefectDetailContainer 加载 DefectReviewControl
- 缺陷矩形点击: 同上，反之高亮列表项
- 上一张/下一张: 仅切换大面图片（同一产品内的不同角度），不关闭弹窗
- 关闭按钮: 复用现有逻辑
- 小地图: **保持原样**，纯导航，不叠加缺陷标记

**缺陷列表 ItemTemplate**:
- 缺陷类型（彩色标签）
- 置信度百分比
- 复判状态图标（未复判/OK/NG）
- 坐标信息

### 5.3 第三层：DefectReviewControl（新增）

**新增文件**: `View/StateCards/DefectReviewControl.xaml`, `View/StateCards/DefectReviewControl.xaml.cs`

**命名空间**: `ZenergyBFSI.View.StateCards`

**XAML 内容**:
- 缺陷裁剪图（Image, 固定 300×300, Stretch=Uniform）
- 缺陷信息文本（类型、置信度、坐标）
- 两个 ToggleButton（OK / NG）
- 操作员 TextBox（必填）
- 备注 TextBox
- 提交按钮 + 返回按钮

**代码接口**:
```csharp
// 构造函数
public DefectReviewControl(string originalImagePath, DefectRegion defect)

// 输出事件
public event Action<ReviewResult> ReviewSubmitted;

public class ReviewResult
{
    public string DefectId { get; set; }
    public string Result { get; set; }   // "OK" / "NG"
    public string User { get; set; }
    public string Comment { get; set; }
}
```

**内部逻辑**:
- 构造函数中从 `originalImagePath` 加载原图 → 按坐标裁剪 → `CroppedBitmap` → 20% padding → 缩放到 300×300
- 提交按钮: 校验操作员非空 → 触发 `ReviewSubmitted` 事件
- 返回按钮: 将自身从父容器移除，回到缺陷列表视图
- 不缓存裁剪图，每次新建时从原图动态裁剪

## 六、事件流

```
第一层卡片点击 → OpenDefectMap(img)
  → 显示 GridOverlay + 加载大图 + DrawDefectRects

第二层缺陷点击 → DefectDetailContainer.Content = new DefectReviewControl(path, defect)
  → 显示第三层

第三层提交 → DefectReviewControl.ReviewSubmitted
  → UC_InspectionView 转发 SaveReviewRequested(args with DefectId)
  → 外部服务处理
  → OnReviewSaved(defectId, result, user, comment)
  → 更新 ImageRecord.Defects 中对应项的 Status
  → RefreshDefectList() + RedrawDefectRects()
  → 移除 DefectReviewControl，回到缺陷列表
```

**OnReviewSaved 新增重载**:
```csharp
// 整图复检（兼容旧接口）
void OnReviewSaved(string imageId, string result, string user, string comment);

// 缺陷级复检（新增）
void OnReviewSaved(string defectId, string result, string user, string comment);
```

## 七、文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Model/InspectionUtils.cs` | 修改 | 新增 DefectRegion 类、ReviewStatus 枚举、ImageRecord.Defects、GenerateDefaultDefects() |
| `Model/Messages/DashboardMessages.cs` | 修改 | SaveReviewArgs 增加 DefectId |
| `View/StateCards/UC_InspectionView.xaml` | 修改 | GridOverlay 内部重构为左右分栏 + DefectCanvas + DefectDetailContainer |
| `View/StateCards/UC_InspectionView.xaml.cs` | 修改 | 新增 OpenDefectMap/DrawDefectRects/RefreshDefectList/RedrawDefectRects；改卡片点击；修复 RefreshCardFooter |
| `View/StateCards/DefectReviewControl.xaml` | **新增** | 第三层控件 XAML |
| `View/StateCards/DefectReviewControl.xaml.cs` | **新增** | 第三层控件代码 |
| `ZenergyBFSI.csproj` | 修改 | 注册新增的 .xaml/.cs 文件 |

## 八、边界情况

- **无缺陷图片**: DefectCanvas 为空，缺陷列表显示"无缺陷记录"，不阻塞操作
- **网络路径加载失败**: LoadBitmapSafe 已有容错，返回空图；DefectReviewControl 中 catch 异常并显示"图片加载失败"
- **坐标转换异常**: 归一化值超出 0-1 范围时 clamp 到有效范围
- **快速切换图片**: 通过 CancellationTokenSource 取消上一次加载（复用现有 `_loadCts` 机制）
- **第三层覆盖第二层列表**: DefectDetailContainer 位于缺陷列表下方，加载第三层时列表仍可见（滚动查看）
