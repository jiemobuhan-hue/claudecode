# Image Loading Performance Optimization Design

**Date**: 2026-05-25
**Status**: in-progress
**Branch**: master

## Problem

`UC_InspectionView.BuildImageCards()` loads ~60 images per search result, each ~20MB BMP
(1149×7281 px). Full-resolution `BitmapImage` decode runs synchronously on the UI thread
via `Dispatcher.InvokeAsync`, causing a ~16-second freeze.

**Root cause**: `BuildCard()` line 453 calls `LoadBitmapSafe(img.ImagePath)` without
`DecodePixelWidth`, decoding the full 8.4M-pixel BMP for a 240×170 card display.

## Solution Overview

Three layers applied together:

1. **Decode at thumbnail resolution** — `DecodePixelWidth = 400` in `LoadBitmapSafe`,
   shrinking 8.4M px → ~250K px per image
2. **Background async loading** — `BuildImageCards` renders 60 lightweight placeholder
   cards immediately, then loads real thumbnails via `Task.Run` + `ThumbnailCache`
3. **Local thumbnail cache** — `%LocalAppData%/ZenergyBFSI/thumbcache/`, keyed by
   SHA256(srcPath + lastWriteTime), JPEG Q85 output. First load decodes BMP once;
   subsequent loads hit cached ~80KB JPEG files

## Components

### 1. ThumbnailCache (`Service/ThumbnailCache.cs`)

```
Cache directory: %LocalAppData%/ZenergyBFSI/thumbcache/
Cache key:        SHA256(absolutePath + File.GetLastWriteTime.Ticks)
Cache format:     JPEG, Quality=85, DecodePixelWidth=400
Cache eviction:   Source-file-mtime-bound (auto-rebuilds when source changes);
                  manual Clear() for full purge
```

API:
```
string GetOrCreate(string sourcePath, int decodeWidth)
  Returns cached thumbnail file path.
  On cache miss: loads BMP with DecodePixelWidth → encodes JPEG → writes cache → returns path.

void Clear()
  Deletes all cache files.
```

### 2. BuildImageCards() rewrite

On every call:
1. Cancel any previous `_loadCts` (product switch cancels stale loads)
2. Clear `WpImages.Children`
3. Render 60 `BuildPlaceholderCard()` calls — UI thread, ~50ms, no images loaded
4. `Task.Run(async () => { ... })` background loop:
   - For each image: `ThumbnailCache.GetOrCreate()` → cached path
   - Create `BitmapImage` from cached JPEG path, `Freeze()`
   - `Dispatcher.InvokeAsync(() => ReplaceImage(placeholder, bmp))`
   - Check `token.IsCancellationRequested` before each step

### 3. BuildPlaceholderCard()

Same structural layout as `BuildCard` (Border, StackPanel, info section with angle name,
vision result badge, NG badge), but the image area contains only a gray Grid with the
angle name text — no `Image` control, no `LoadBitmapSafe` call.

### 4. ReplacePlaceholderImage()

Finds the placeholder's gray Grid by Tag, replaces it with an `Image` control whose
`Source` is the loaded `BitmapImage`.

## Data Flow

```
User clicks product in list
  → LvRecords_SelectionChanged
  → LoadImagesRequested → SetProductImages(images)
  → BuildImageCards()
       → Cancel old _loadCts
       → 60 placeholders rendered immediately (<100ms UI work)
       → Background Task.Run loop:
             for each image:
               cached = ThumbnailCache.GetOrCreate(path, 400)
               bmp = new BitmapImage(cached) — from small JPEG, instant
               Dispatcher.Invoke → ReplacePlaceholderImage
  → User switches product mid-load
       → BuildImageCards() called again
       → _loadCts.Cancel() terminates old loop
       → New placeholders + new loop start
```

## Files Changed

| File | Change |
|------|--------|
| `Service/ThumbnailCache.cs` | NEW |
| `View/StateCards/UC_InspectionView.xaml.cs` | Rewrite `BuildImageCards`, add `BuildPlaceholderCard`, `ReplacePlaceholderImage`, `_loadCts` field, `_thumbnailCache` field |
| `ZenergyBFSI.csproj` | Add `ThumbnailCache.cs` to Compile items |

## Expected Performance

| Scenario | Before | After |
|----------|--------|-------|
| First load, 60 images | ~16s freeze | <100ms UI + ~3-5s background progressive |
| Second load, same images | ~16s freeze | <100ms UI + ~0.5s background (cache hit) |
| Switch product mid-load | N/A (can't interrupt) | Instant cancel + new placeholders |

## Edge Cases

- **Source file deleted mid-load**: `GetOrCreate` catches `FileNotFoundException`, returns null; placeholder stays gray
- **Cache disk full**: `GetOrCreate` catches `IOException`, falls back to on-the-fly decode without caching
- **Concurrent loads**: `ThumbnailCache` uses per-key locking so same source file only decodes once even if requested by multiple background iterations simultaneously
