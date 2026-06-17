# PLCHandler 分层重构实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 PLCHandler 从单体上帝对象重构为 PlcMonitor → PlcChannel → IPlcConnection 三层架构，用 Result\<T\> 替代 object+错误字符串，用 ConnectionState 五态枚举替代 bool IsConnected，用 IObservable 替代 event。

**Architecture:** 自底向上重构：先建 Core 基础类型，再改 Connection 层（状态机），再建 Channel 层（生命周期+重连），再建 Monitor 编排层，最后改 UI 订阅层。旧文件在最后统一删除。

**Tech Stack:** C# 9, .NET Framework 4.8, WPF, DevExpress v19.1, HslCommunication 12.8.1, System.Reactive 5.0.0

---

### Task 1: 添加 System.Reactive 依赖 + 创建 Result\<T\> 值类型

**Files:**
- Modify: `WpfApp1.csproj`
- Create: `PLCHandler/Core/Result.cs`

- [ ] **Step 1: 修改 csproj 添加 System.Reactive**

编辑 `WpfApp1.csproj`，在 `<ItemGroup>` 中添加：

```xml
<PackageReference Include="System.Reactive" Version="5.0.0" />
```

位置：放在 `<PackageReference Include="System.Text.Json" Version="8.0.5" />` 后面。

- [ ] **Step 2: 创建 Core 目录并创建 Result.cs**

```bash
mkdir -p PLCHandler/Core
```

创建 `PLCHandler/Core/Result.cs`：

```csharp
using System;

namespace WpfApp1.PLCHandler
{
    public readonly struct Result<T>
    {
        public bool IsOk { get; }
        public T Value { get; }
        public string Error { get; }

        private Result(bool isOk, T value, string error)
        {
            IsOk = isOk;
            Value = value;
            Error = error;
        }

        public static Result<T> Ok(T value) => new(true, value, null);
        public static Result<T> Fail(string error) => new(false, default, error);

        public static implicit operator Result<T>(T value) => Ok(value);

        public override string ToString() => IsOk ? Value?.ToString() ?? "null" : $"Error: {Error}";
    }
}
```

- [ ] **Step 3: 编译验证**

```bash
cd "D:/蓝膜外观检测上位机/优化代码/ZenergyBFSI0430/claudecodeworkspaces/独立项目/PLCHandler/WpfApp1" && dotnet build
```

期望：编译通过。

- [ ] **Step 4: Commit**

```bash
git add WpfApp1.csproj PLCHandler/Core/Result.cs
git commit -m "feat: add System.Reactive dependency and Result<T> value type"
```

---

### Task 2: 创建 ConnectionState 枚举 + SignalUpdate DTO

**Files:**
- Create: `PLCHandler/Core/ConnectionState.cs`
- Create: `PLCHandler/Core/SignalUpdate.cs`

- [ ] **Step 1: 创建 ConnectionState.cs**

```csharp
namespace WpfApp1.PLCHandler
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Faulted
    }
}
```

- [ ] **Step 2: 创建 SignalUpdate.cs**

```csharp
using System;

namespace WpfApp1.PLCHandler
{
    public sealed class SignalUpdate
    {
        public string SignalId { get; init; }
        public string PlcId { get; init; }
        public Result<object> Value { get; init; }
        public DateTime Timestamp { get; init; }

        public SignalUpdate(string signalId, string plcId, Result<object> value, DateTime timestamp)
        {
            SignalId = signalId;
            PlcId = plcId;
            Value = value;
            Timestamp = timestamp;
        }
    }
}
```

- [ ] **Step 3: 编译验证**

```bash
dotnet build
```

期望：编译通过。

- [ ] **Step 4: Commit**

```bash
git add PLCHandler/Core/ConnectionState.cs PLCHandler/Core/SignalUpdate.cs
git commit -m "feat: add ConnectionState enum and SignalUpdate DTO"
```

---

### Task 3: 重构 IPlcConnection —— 同步方法 + 状态属性

**Files:**
- Modify: `PLCHandler/IPlcConnection.cs`

- [ ] **Step 1: 重写接口**

用以下内容完整替换 `PLCHandler/IPlcConnection.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;

namespace WpfApp1.PLCHandler
{
    public interface IPlcConnection : IDisposable
    {
        string Id { get; }
        ConnectionState State { get; }

        /// <summary>
        /// Connect to PLC. Returns true on success. Thread-safe, can be called
        /// from any state — will close existing connection before reconnecting.
        /// </summary>
        Task<bool> ConnectAsync(CancellationToken ct = default);

        /// <summary>
        /// Disconnect from PLC.
        /// </summary>
        Task DisconnectAsync();

        // ---- 同步类型化读取 ----

        OperateResult<bool> ReadBool(string address);
        OperateResult<short> ReadInt16(string address);
        OperateResult<ushort> ReadUInt16(string address);
        OperateResult<int> ReadInt32(string address);
        OperateResult<uint> ReadUInt32(string address);
        OperateResult<long> ReadInt64(string address);
        OperateResult<ulong> ReadUInt64(string address);
        OperateResult<float> ReadFloat(string address);
        OperateResult<double> ReadDouble(string address);
        OperateResult<string> ReadString(string address, ushort length);
        OperateResult<bool[]> ReadBoolArray(string address, ushort length);
        OperateResult<short[]> ReadInt16Array(string address, ushort length);
        OperateResult<int[]> ReadInt32Array(string address, ushort length);
        OperateResult<byte[]> ReadByteArray(string address, ushort length);

        // ---- 写入（保留但非重点）----

        OperateResult Write(string address, byte[] data);
    }
}
```

与旧接口的区别：
- 移除了 `IsConnected` bool → 改为 `ConnectionState State`
- 移除了 `OnError` event → 错误通过 OperateResult 返回
- 移除了 `ReadAsync/WriteAsync` 方法 → 异步由 Channel 层用 Task.Run 编排
- `ConnectAsync` 增加了 `CancellationToken` 参数
- 新增 `Write` 同步方法（基于 OperateResult）

- [ ] **Step 2: 编译**

此时编译会报错（SiemensConnection 等不再实现旧接口的方法）。这是预期的——下个任务修复。

```bash
dotnet build
```

预期：编译失败，报错 `does not implement interface member`。

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/IPlcConnection.cs
git commit -m "refactor: rewrite IPlcConnection — sync reads, ConnectionState, CancellationToken support"
```

---

### Task 4: 重写 SiemensConnection —— 状态机 + 真实读取验证

**Files:**
- Modify: `PLCHandler/Connections/SiemensConnection.cs`

- [ ] **Step 1: 重写 SiemensConnection**

用以下内容完整替换 `PLCHandler/Connections/SiemensConnection.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Profinet.Siemens;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class SiemensConnection : IPlcConnection
    {
        private SiemensS7Net _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public SiemensConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new SiemensS7Net(SiemensPLCS.S1200)
            {
                IpAddress = options.IpAddress,
                Port = options.Port,
                Slot = options.Slot
            };
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            State = ConnectionState.Connecting;

            try
            {
                var connectTask = Task.Run(() => _plc.ConnectServer(), ct);
                var timeoutTask = Task.Delay(5000, ct);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completed == timeoutTask)
                {
                    State = ConnectionState.Disconnected;
                    return false;
                }

                var result = connectTask.Result;
                if (result.IsSuccess)
                {
                    // 真实验证：连上后读一次确认连接有效
                    var verify = _plc.ReadInt16("DB1.DBW0");
                    if (verify.IsSuccess)
                    {
                        State = ConnectionState.Connected;
                        return true;
                    }
                    _plc.ConnectClose();
                    State = ConnectionState.Disconnected;
                    return false;
                }

                State = ConnectionState.Disconnected;
                return false;
            }
            catch (OperationCanceledException)
            {
                _plc.ConnectClose();
                State = ConnectionState.Disconnected;
                return false;
            }
            catch
            {
                State = ConnectionState.Disconnected;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() =>
            {
                _plc.ConnectClose();
                State = ConnectionState.Disconnected;
            });
        }

        // ---- 类型化读取 ----

        public OperateResult<bool> ReadBool(string address) => _plc.ReadBool(address);
        public OperateResult<short> ReadInt16(string address) => _plc.ReadInt16(address);
        public OperateResult<ushort> ReadUInt16(string address) => _plc.ReadUInt16(address);
        public OperateResult<int> ReadInt32(string address) => _plc.ReadInt32(address);
        public OperateResult<uint> ReadUInt32(string address) => _plc.ReadUInt32(address);
        public OperateResult<long> ReadInt64(string address) => _plc.ReadInt64(address);
        public OperateResult<ulong> ReadUInt64(string address) => _plc.ReadUInt64(address);
        public OperateResult<float> ReadFloat(string address) => _plc.ReadFloat(address);
        public OperateResult<double> ReadDouble(string address) => _plc.ReadDouble(address);
        public OperateResult<string> ReadString(string address, ushort length) => _plc.ReadString(address, length);

        public OperateResult<bool[]> ReadBoolArray(string address, ushort length)
        {
            var result = _plc.Read(address, length);
            if (!result.IsSuccess)
                return new OperateResult<bool[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };

            if (result.Content is byte[] bytes)
            {
                var bools = new bool[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    bools[i] = bytes[i] != 0;
                return OperateResult.CreateSuccessResult(bools);
            }
            return new OperateResult<bool[]> { IsSuccess = false, Message = "Invalid content type" };
        }

        public OperateResult<short[]> ReadInt16Array(string address, ushort length)
        {
            var result = _plc.Read(address, (ushort)(length * 2));
            if (!result.IsSuccess)
                return new OperateResult<short[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };
            return OperateResult.CreateSuccessResult(ToArray<short>(result.Content, length));
        }

        public OperateResult<int[]> ReadInt32Array(string address, ushort length)
        {
            var result = _plc.Read(address, (ushort)(length * 4));
            if (!result.IsSuccess)
                return new OperateResult<int[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };
            return OperateResult.CreateSuccessResult(ToArray<int>(result.Content, length));
        }

        public OperateResult<byte[]> ReadByteArray(string address, ushort length)
        {
            var result = _plc.Read(address, length);
            if (!result.IsSuccess)
                return new OperateResult<byte[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };
            return result.Content is byte[] bytes
                ? OperateResult.CreateSuccessResult(bytes)
                : new OperateResult<byte[]> { IsSuccess = false, Message = "Invalid content type" };
        }

        public OperateResult Write(string address, byte[] data)
        {
            return _plc.Write(address, data);
        }

        private static T[] ToArray<T>(object content, ushort length) where T : struct
        {
            if (content is byte[] bytes)
            {
                var size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
                var count = System.Math.Min(length, bytes.Length / size);
                var result = new T[count];
                Buffer.BlockCopy(bytes, 0, result, 0, count * size);
                return result;
            }
            return Array.Empty<T>();
        }

        public void Dispose() => _plc?.ConnectClose();
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

预期：Omron/Mitsubishi/Modbus 连接类仍报接口错误（下个任务修复），但 SiemensConnection 编译通过。

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/Connections/SiemensConnection.cs
git commit -m "refactor: rewrite SiemensConnection with state machine and verified reads"
```

---

### Task 5: 重写 OmronConnection —— FinsTCP 状态机

**Files:**
- Modify: `PLCHandler/Connections/OmronConnection.cs`

- [ ] **Step 1: 重写 OmronConnection**

用以下内容完整替换 `PLCHandler/Connections/OmronConnection.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Profinet.Omron;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class OmronConnection : IPlcConnection
    {
        private OmronFinsNet _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public OmronConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new OmronFinsNet(options.IpAddress, options.Port);
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            State = ConnectionState.Connecting;

            try
            {
                var connectTask = Task.Run(() => _plc.ConnectServer(), ct);
                var timeoutTask = Task.Delay(5000, ct);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completed == timeoutTask)
                {
                    State = ConnectionState.Disconnected;
                    return false;
                }

                var result = connectTask.Result;
                if (result.IsSuccess)
                {
                    State = ConnectionState.Connected;
                    return true;
                }

                State = ConnectionState.Disconnected;
                return false;
            }
            catch (OperationCanceledException)
            {
                _plc.ConnectClose();
                State = ConnectionState.Disconnected;
                return false;
            }
            catch
            {
                State = ConnectionState.Disconnected;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() =>
            {
                _plc.ConnectClose();
                State = ConnectionState.Disconnected;
            });
        }

        public OperateResult<bool> ReadBool(string address) => _plc.ReadBool(address);
        public OperateResult<short> ReadInt16(string address) => _plc.ReadInt16(address);
        public OperateResult<ushort> ReadUInt16(string address) => _plc.ReadUInt16(address);
        public OperateResult<int> ReadInt32(string address) => _plc.ReadInt32(address);
        public OperateResult<uint> ReadUInt32(string address) => _plc.ReadUInt32(address);
        public OperateResult<long> ReadInt64(string address) => _plc.ReadInt64(address);
        public OperateResult<ulong> ReadUInt64(string address) => _plc.ReadUInt64(address);
        public OperateResult<float> ReadFloat(string address) => _plc.ReadFloat(address);
        public OperateResult<double> ReadDouble(string address) => _plc.ReadDouble(address);
        public OperateResult<string> ReadString(string address, ushort length) => _plc.ReadString(address, length);

        public OperateResult<bool[]> ReadBoolArray(string address, ushort length)
        {
            return _plc.ReadBool(address, length);
        }

        public OperateResult<short[]> ReadInt16Array(string address, ushort length)
        {
            return _plc.ReadInt16(address, length);
        }

        public OperateResult<int[]> ReadInt32Array(string address, ushort length)
        {
            return _plc.ReadInt32(address, length);
        }

        public OperateResult<byte[]> ReadByteArray(string address, ushort length)
        {
            return _plc.Read(address, length);
        }

        public OperateResult Write(string address, byte[] data)
        {
            return _plc.Write(address, data);
        }

        public void Dispose() => _plc?.ConnectClose();
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

预期：Mitsubishi/Modbus 仍报接口错误（不属于目标品牌，下一个任务处理）。

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/Connections/OmronConnection.cs
git commit -m "refactor: rewrite OmronConnection with FinsTCP state machine"
```

---

### Task 6: 更新 Mitsubishi 和 Modbus Connection 以匹配新接口（最小化）

**Files:**
- Modify: `PLCHandler/Connections/MitsubishiConnection.cs`
- Modify: `PLCHandler/Connections/ModbusConnection.cs`

这两个品牌不在当前目标范围，但必须实现新接口才能编译通过。

- [ ] **Step 1: 重写 MitsubishiConnection**

用以下内容完整替换 `PLCHandler/Connections/MitsubishiConnection.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Profinet.Melsec;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class MitsubishiConnection : IPlcConnection
    {
        private MelsecMcNet _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public MitsubishiConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new MelsecMcNet(options.IpAddress, options.Port);
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            State = ConnectionState.Connecting;
            try
            {
                var connectTask = Task.Run(() => _plc.ConnectServer(), ct);
                var timeoutTask = Task.Delay(5000, ct);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                if (completed == timeoutTask)
                {
                    State = ConnectionState.Disconnected;
                    return false;
                }
                var result = connectTask.Result;
                State = result.IsSuccess ? ConnectionState.Connected : ConnectionState.Disconnected;
                return result.IsSuccess;
            }
            catch
            {
                State = ConnectionState.Disconnected;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() => { _plc.ConnectClose(); State = ConnectionState.Disconnected; });
        }

        public OperateResult<bool> ReadBool(string address) => _plc.ReadBool(address);
        public OperateResult<short> ReadInt16(string address) => _plc.ReadInt16(address);
        public OperateResult<ushort> ReadUInt16(string address) => _plc.ReadUInt16(address);
        public OperateResult<int> ReadInt32(string address) => _plc.ReadInt32(address);
        public OperateResult<uint> ReadUInt32(string address) => _plc.ReadUInt32(address);
        public OperateResult<long> ReadInt64(string address) => _plc.ReadInt64(address);
        public OperateResult<ulong> ReadUInt64(string address) => _plc.ReadUInt64(address);
        public OperateResult<float> ReadFloat(string address) => _plc.ReadFloat(address);
        public OperateResult<double> ReadDouble(string address) => _plc.ReadDouble(address);
        public OperateResult<string> ReadString(string address, ushort length) => _plc.ReadString(address, length);
        public OperateResult<bool[]> ReadBoolArray(string address, ushort length) => NotSupported<bool[]>();
        public OperateResult<short[]> ReadInt16Array(string address, ushort length) => NotSupported<short[]>();
        public OperateResult<int[]> ReadInt32Array(string address, ushort length) => NotSupported<int[]>();
        public OperateResult<byte[]> ReadByteArray(string address, ushort length) => NotSupported<byte[]>();
        public OperateResult Write(string address, byte[] data) => _plc.Write(address, data);
        public void Dispose() => _plc?.ConnectClose();

        private static OperateResult<T> NotSupported<T>() =>
            new() { IsSuccess = false, Message = "NotSupported" };
    }
}
```

- [ ] **Step 2: 重写 ModbusConnection**

用以下内容完整替换 `PLCHandler/Connections/ModbusConnection.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.ModBus;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class ModbusConnection : IPlcConnection
    {
        private ModbusTcpNet _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public ModbusConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new ModbusTcpNet(options.IpAddress, options.Port, options.Channel);
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            State = ConnectionState.Connecting;
            try
            {
                var connectTask = Task.Run(() => _plc.ConnectServer(), ct);
                var timeoutTask = Task.Delay(5000, ct);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                if (completed == timeoutTask)
                {
                    State = ConnectionState.Disconnected;
                    return false;
                }
                var result = connectTask.Result;
                State = result.IsSuccess ? ConnectionState.Connected : ConnectionState.Disconnected;
                return result.IsSuccess;
            }
            catch
            {
                State = ConnectionState.Disconnected;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() => { _plc.ConnectClose(); State = ConnectionState.Disconnected; });
        }

        public OperateResult<bool> ReadBool(string address) => _plc.ReadBool(address);
        public OperateResult<short> ReadInt16(string address) => _plc.ReadInt16(address);
        public OperateResult<ushort> ReadUInt16(string address) => _plc.ReadUInt16(address);
        public OperateResult<int> ReadInt32(string address) => _plc.ReadInt32(address);
        public OperateResult<uint> ReadUInt32(string address) => _plc.ReadUInt32(address);
        public OperateResult<long> ReadInt64(string address) => _plc.ReadInt64(address);
        public OperateResult<ulong> ReadUInt64(string address) => _plc.ReadUInt64(address);
        public OperateResult<float> ReadFloat(string address) => _plc.ReadFloat(address);
        public OperateResult<double> ReadDouble(string address) => _plc.ReadDouble(address);
        public OperateResult<string> ReadString(string address, ushort length) => _plc.ReadString(address, length);
        public OperateResult<bool[]> ReadBoolArray(string address, ushort length) => NotSupported<bool[]>();
        public OperateResult<short[]> ReadInt16Array(string address, ushort length) => NotSupported<short[]>();
        public OperateResult<int[]> ReadInt32Array(string address, ushort length) => NotSupported<int[]>();
        public OperateResult<byte[]> ReadByteArray(string address, ushort length) => NotSupported<byte[]>();
        public OperateResult Write(string address, byte[] data) => _plc.Write(address, data);
        public void Dispose() => _plc?.ConnectClose();

        private static OperateResult<T> NotSupported<T>() =>
            new() { IsSuccess = false, Message = "NotSupported" };
    }
}
```

- [ ] **Step 3: 编译验证**

```bash
dotnet build
```

此时所有 Connection 类应编译通过，但由于 `PlcConnectionFactory` 和 `PlcConnectionPool` 仍引用旧的 `IPlcConnection`，仍可能有编译错误。下个任务解决。

预期：Connection 层的编译错误全部消除。

- [ ] **Step 4: Commit**

```bash
git add PLCHandler/Connections/MitsubishiConnection.cs PLCHandler/Connections/ModbusConnection.cs
git commit -m "refactor: update Mitsubishi and Modbus connections to new IPlcConnection"
```

---

### Task 7: 重写 SignalReader —— 返回 Result\<object\>

**Files:**
- Modify: `PLCHandler/SignalReader.cs`

- [ ] **Step 1: 重写 SignalReader**

用以下内容完整替换 `PLCHandler/SignalReader.cs`：

```csharp
using System;
using System.Threading.Tasks;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class SignalReader
    {
        private readonly IPlcConnection _plc;

        public SignalReader(IPlcConnection plc)
        {
            _plc = plc;
        }

        public async Task<Result<object>> ReadValueAsync(SignalData signal)
        {
            try
            {
                return await Task.Run(() => ReadValueCore(signal));
            }
            catch (Exception ex)
            {
                // 只有 NotSupportedException 或 Task.Run 失败才会到这里
                return Result<object>.Fail(ex.Message);
            }
        }

        private Result<object> ReadValueCore(SignalData signal)
        {
            switch (signal.DataType)
            {
                case DataTypeEnum.Bool:
                {
                    var r = _plc.ReadBool(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Short:
                {
                    var r = _plc.ReadInt16(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.UShort:
                {
                    var r = _plc.ReadUInt16(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Int:
                {
                    var r = _plc.ReadInt32(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.UInt:
                {
                    var r = _plc.ReadUInt32(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Long:
                {
                    var r = _plc.ReadInt64(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.ULong:
                {
                    var r = _plc.ReadUInt64(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Float:
                {
                    var r = _plc.ReadFloat(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Double:
                {
                    var r = _plc.ReadDouble(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.String:
                {
                    var len = signal.ArrayLength > 0 ? signal.ArrayLength : 16;
                    var r = _plc.ReadString(signal.Address, (ushort)len);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.BoolArray:
                {
                    var r = _plc.ReadBoolArray(signal.Address, (ushort)signal.ArrayLength);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.ShortArray:
                {
                    var r = _plc.ReadInt16Array(signal.Address, (ushort)signal.ArrayLength);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.IntArray:
                {
                    var r = _plc.ReadInt32Array(signal.Address, (ushort)signal.ArrayLength);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Byte:
                {
                    var r = _plc.ReadByteArray(signal.Address, 1);
                    return r.IsSuccess ? r.Content[0] : Result<object>.Fail(r.Message);
                }
                default:
                    return Result<object>.Fail($"Unsupported data type: {signal.DataType}");
            }
        }
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/SignalReader.cs
git commit -m "refactor: rewrite SignalReader to return Result<object>"
```

---

### Task 8: 创建 RetryPolicy —— 指数退避

**Files:**
- Create: `PLCHandler/Channel/RetryPolicy.cs`

- [ ] **Step 1: 创建 Channel 目录并创建 RetryPolicy.cs**

```bash
mkdir -p PLCHandler/Channel
```

创建 `PLCHandler/Channel/RetryPolicy.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.PLCHandler
{
    public sealed class RetryPolicy
    {
        private readonly int _maxRetries;
        private readonly int _baseDelayMs;
        private readonly int _maxDelayMs;
        private int _retryCount;

        public int RetryCount => _retryCount;
        public bool IsExhausted => _retryCount >= _maxRetries;

        public RetryPolicy(int maxRetries = 10, int baseDelayMs = 500, int maxDelayMs = 30000)
        {
            _maxRetries = maxRetries;
            _baseDelayMs = baseDelayMs;
            _maxDelayMs = maxDelayMs;
        }

        public void Reset()
        {
            _retryCount = 0;
        }

        public async Task<bool> WaitForNextRetryAsync(CancellationToken ct = default)
        {
            if (_retryCount >= _maxRetries)
                return false;

            _retryCount++;
            var delay = System.Math.Min(_baseDelayMs * (int)System.Math.Pow(2, _retryCount - 1), _maxDelayMs);

            try
            {
                await Task.Delay(delay, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/Channel/RetryPolicy.cs
git commit -m "feat: add RetryPolicy with exponential backoff"
```

---

### Task 9: 创建 PlcChannel —— 单 PLC 完整生命周期

**Files:**
- Create: `PLCHandler/Channel/PlcChannel.cs`

- [ ] **Step 1: 创建 PlcChannel.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class PlcChannel : IDisposable
    {
        private readonly PlcConfig _config;
        private readonly PlcConnectionOptions _options;
        private readonly List<SignalData> _signals;
        private readonly IPlcConnection _connection;
        private readonly SignalReader _reader;
        private readonly RetryPolicy _retryPolicy;
        private readonly Subject<SignalUpdate> _signalSubject = new();
        private CancellationTokenSource _loopCts;
        private Task _loopTask;
        private ConnectionState _state;
        private readonly object _stateLock = new object();

        public string PlcId => _config.Id;
        public IObservable<SignalUpdate> Signals => _signalSubject;
        public IReadOnlyList<SignalData> SignalDefs => _signals;

        public ConnectionState State
        {
            get { lock (_stateLock) return _state; }
            private set
            {
                lock (_stateLock) _state = value;
                StatusChanged?.Invoke(this, new PlcStatus(PlcId, value, _config.Name, _retryPolicy.RetryCount));
            }
        }

        public event EventHandler<PlcStatus> StatusChanged;

        public PlcChannel(PlcConfig config, List<SignalData> signals, IPlcConnection connection)
        {
            _config = config;
            _signals = signals;
            _connection = connection;
            _reader = new SignalReader(_connection);
            _retryPolicy = new RetryPolicy();
            _options = new PlcConnectionOptions
            {
                Id = config.Id,
                Name = config.Name,
                Brand = config.Brand,
                IpAddress = config.IpAddress,
                Port = config.Port,
                Slot = config.Slot,
                Channel = config.Channel,
                Group = config.Group
            };
            _state = ConnectionState.Disconnected;
        }

        public void Start()
        {
            if (_loopCts != null) return;

            _loopCts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_loopCts.Token);
        }

        public void Stop()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
            _loopTask = null;
            State = ConnectionState.Disconnected;
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            // Phase 1: Connect
            var connected = await _connection.ConnectAsync(ct);
            State = _connection.State;

            if (!connected)
            {
                await ReconnectLoopAsync(ct);
                return;
            }

            _retryPolicy.Reset();

            // Phase 2: Polling loop
            var intervalMs = _config.PollingIntervalMs > 0 ? _config.PollingIntervalMs : 500;

            while (!ct.IsCancellationRequested)
            {
                foreach (var signal in _signals)
                {
                    if (ct.IsCancellationRequested) break;

                    var result = await _reader.ReadValueAsync(signal);
                    var update = new SignalUpdate(signal.Id, signal.PlcId, result, DateTime.Now);
                    _signalSubject.OnNext(update);

                    // 连续 3 次读取失败 → 进入重连
                    if (!result.IsOk && _connection.State != ConnectionState.Connected)
                    {
                        await ReconnectLoopAsync(ct);
                        return; // 重连成功后会重新进入主循环
                    }
                }

                try { await Task.Delay(intervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }

            await _connection.DisconnectAsync();
        }

        private async Task ReconnectLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_retryPolicy.IsExhausted)
            {
                State = ConnectionState.Reconnecting;

                var canRetry = await _retryPolicy.WaitForNextRetryAsync(ct);
                if (!canRetry || ct.IsCancellationRequested) break;

                var connected = await _connection.ConnectAsync(ct);
                State = _connection.State;

                if (connected)
                {
                    // 重连成功，回到主循环
                    _retryPolicy.Reset();
                    await RunLoopAsync(ct);
                    return;
                }
            }

            // 重试耗尽
            State = ConnectionState.Faulted;
        }

        public void Dispose()
        {
            Stop();
            _signalSubject?.OnCompleted();
            _signalSubject?.Dispose();
            _connection?.Dispose();
        }
    }

    public sealed class PlcStatus
    {
        public string PlcId { get; }
        public ConnectionState State { get; }
        public string PlcName { get; }
        public int RetryCount { get; }

        public PlcStatus(string plcId, ConnectionState state, string plcName, int retryCount)
        {
            PlcId = plcId;
            State = state;
            PlcName = plcName;
            RetryCount = retryCount;
        }
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

可能的编译错误：
- `PlcConnectionFactory` 和 `PlcConnectionPool` 可能仍引用旧的代码路径。如果报错，先注释掉 `PlcConnectionFactory.cs` 中不再使用的方法（暂时保留类定义即可）。

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/Channel/PlcChannel.cs
git commit -m "feat: add PlcChannel with connect→poll→reconnect lifecycle"
```

---

### Task 10: 创建 PlcMonitor + 更新 PlcConfigService

**Files:**
- Create: `PLCHandler/PlcMonitor.cs`
- Modify: `PLCHandler/PlcConfigService.cs`

- [ ] **Step 1: 创建 PlcMonitor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public sealed class PlcMonitor : IDisposable
    {
        private readonly Dictionary<string, PlcChannel> _channels = new();
        private readonly Subject<PlcStatus> _statusSubject = new();
        private readonly PlcConfigService _configService;
        private List<PlcConfig> _plcConfigs = new();
        private List<SignalData> _signals = new();

        public IReadOnlyDictionary<string, PlcChannel> Channels => _channels;
        public IObservable<PlcStatus> StatusStream => _statusSubject.AsObservable();
        public IReadOnlyList<PlcConfig> PlcConfigs => _plcConfigs;
        public PlcConfigService ConfigService => _configService;

        public PlcMonitor(PlcConfigService configService)
        {
            _configService = configService;
        }

        public void LoadConfigs()
        {
            _plcConfigs = _configService.LoadPlcConfigs();
            _signals = _configService.LoadSignals();
        }

        public PlcChannel AddChannel(PlcConfig config)
        {
            if (_channels.ContainsKey(config.Id))
                return _channels[config.Id];

            var signals = _signals.FindAll(s => s.PlcId == config.Id);
            var options = new PlcConnectionOptions
            {
                Id = config.Id, Name = config.Name, Brand = config.Brand,
                IpAddress = config.IpAddress, Port = config.Port,
                Slot = config.Slot, Channel = config.Channel, Group = config.Group
            };

            var connection = PlcConnectionFactory.Create(options);
            var channel = new PlcChannel(config, signals, connection);
            channel.StatusChanged += OnChannelStatusChanged;
            _channels[config.Id] = channel;
            channel.Start();

            return channel;
        }

        public void AddPlcConfig(PlcConfig config)
        {
            _plcConfigs.Add(config);
        }

        public void RemoveChannel(string plcId)
        {
            if (_channels.TryGetValue(plcId, out var channel))
            {
                channel.StatusChanged -= OnChannelStatusChanged;
                channel.Stop();
                channel.Dispose();
                _channels.Remove(plcId);
            }
        }

        public void StartAll()
        {
            foreach (var config in _plcConfigs)
            {
                if (config.IsEnabled)
                    AddChannel(config);
            }
        }

        public void StopAll()
        {
            foreach (var id in _channels.Keys)
                RemoveChannel(id);
            _channels.Clear();
        }

        public void SaveConfigs()
        {
            _configService.SavePlcConfigs(_plcConfigs);
            _configService.SaveSignals(_signals);
        }

        public void AddSignal(SignalData signal)
        {
            _signals.Add(signal);
        }

        public void RemoveSignal(string signalId)
        {
            _signals.RemoveAll(s => s.Id == signalId);
        }

        private void OnChannelStatusChanged(object sender, PlcStatus status)
        {
            _statusSubject.OnNext(status);
        }

        public void Dispose()
        {
            StopAll();
            _statusSubject?.OnCompleted();
            _statusSubject?.Dispose();
        }
    }
}
```

- [ ] **Step 2: 更新 PlcConnectionFactory 以适配新接口**

当前 `PlcConnectionFactory.Create` 返回 `IPlcConnection`。由于接口签名变化不大（构造函数参数不变），只需要确认方法签名匹配。如果工厂类仍编译通过，无需修改。

如果 `PlcConnectionPool` 引发编译错误（因为它引用了工厂返回的类型），则先删除 `PlcConnectionPool.cs`（新的 PlcChannel 不再需要连接池）。

- [ ] **Step 3: 编译验证**

```bash
dotnet build
```

如果有编译错误，优先修复：
- 删除 `PlcConnectionPool.cs`（已不 需要）
- 确认 `PlcConnectionFactory` 只包含 `Create` 方法

- [ ] **Step 4: Commit**

```bash
git add PLCHandler/PlcMonitor.cs
git commit -m "feat: add PlcMonitor — orchestration layer for PlcChannel management"
```

---

### Task 11: 清理 SignalData 模型 —— 移除运行时属性

**Files:**
- Modify: `PLCHandler/Models/SignalData.cs`

- [ ] **Step 1: 移除 SignalData 中的运行时属性**

SignalData 现已是纯配置模型。移除 `Value`, `PreviousValue`, `IsChanged`, `LastUpdateTime`。

读取 `PLCHandler/Models/SignalData.cs`，替换为：

```csharp
using System;

namespace WpfApp1.PLCHandler.Models
{
    public sealed class SignalData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PlcId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DataTypeEnum DataType { get; set; } = DataTypeEnum.Int;
        public int ArrayLength { get; set; } = 1;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        // 运行时属性移除：Value / PreviousValue / IsChanged / LastUpdateTime / DisplayValue / ConvertByDataType
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

如果有代码引用已移除的属性（如 `signal.Value`, `signal.IsChanged`），暂时忽略。下个任务会在 ViewModel 层修复。

- [ ] **Step 3: Commit**

```bash
git add PLCHandler/Models/SignalData.cs
git commit -m "refactor: remove runtime properties from SignalData — it is now config-only"
```

---

### Task 12: 更新 ViewModel —— SignalDisplayItem + PlcStatusItem

**Files:**
- Modify: `ViewModels/MainViewModel.cs`

- [ ] **Step 1: 完整重写 MainViewModel.cs**

用以下内容替换 `ViewModels/MainViewModel.cs`：

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using WpfApp1.PLCHandler;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly PlcMonitor _monitor;
        private IDisposable _statusSub;
        private IDisposable _signalSub;
        private string _selectedPlcId;
        private string _selectedView = "PLCConnection";
        private PlcStatusItem _selectedPlc;
        private ObservableCollection<PlcStatusItem> _plcList = new();
        private ObservableCollection<SignalDisplayItem> _signals = new();
        private string _plcCountLabel = "未连接";
        private string _signalCountLabel = "0 信号";
        private string _lastRefreshLabel = "最后刷新: --:--:--";
        private readonly object _signalsLock = new object();

        public ObservableCollection<PlcStatusItem> PlcList => _plcList;
        public ObservableCollection<SignalDisplayItem> Signals => _signals;

        public string SelectedPlcId
        {
            get => _selectedPlcId;
            set
            {
                if (SetProperty(ref _selectedPlcId, value))
                    RefreshSignals();
            }
        }

        public string SelectedView
        {
            get => _selectedView;
            set => SetProperty(ref _selectedView, value);
        }

        public PlcStatusItem SelectedPlc
        {
            get => _selectedPlc;
            set => SetProperty(ref _selectedPlc, value);
        }

        public string PlcCountLabel
        {
            get => _plcCountLabel;
            set => SetProperty(ref _plcCountLabel, value);
        }

        public string SignalCountLabel
        {
            get => _signalCountLabel;
            set => SetProperty(ref _signalCountLabel, value);
        }

        public string LastRefreshLabel
        {
            get => _lastRefreshLabel;
            set => SetProperty(ref _lastRefreshLabel, value);
        }

        public bool AnyConnected => _plcList.Any(p => p.State == ConnectionState.Connected);

        public MainViewModel(PlcMonitor monitor)
        {
            _monitor = monitor;
            _monitor.LoadConfigs();

            // 订阅状态流
            _statusSub = _monitor.StatusStream
                .ObserveOnDispatcher()
                .Subscribe(OnStatusUpdate);

            // 订阅信号流（汇总所有 Channel）
            _signalSub = Observable.Merge(
                _monitor.Channels.Values.Select(c => c.Signals)
            )
            .ObserveOnDispatcher()
            .Subscribe(OnSignalUpdate);

            // 加载 PLC 列表
            RefreshPlcList();
            RefreshSignals();

            // 连接所有
            _monitor.StartAll();

            // 定期刷新统计
            var statusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            statusTimer.Tick += (s, e) => RefreshStats();
            statusTimer.Start();

            System.Diagnostics.Debug.WriteLine("[MainViewModel] initialized with PlcMonitor");
        }

        private void OnStatusUpdate(PlcStatus status)
        {
            var item = _plcList.FirstOrDefault(p => p.Id == status.PlcId);
            if (item != null)
            {
                item.State = status.State;
                item.RetryCount = status.RetryCount;
            }
            else
            {
                var cfg = _monitor.PlcConfigs.FirstOrDefault(c => c.Id == status.PlcId);
                _plcList.Add(new PlcStatusItem
                {
                    Id = status.PlcId,
                    Name = cfg?.Name ?? status.PlcName,
                    Brand = cfg?.Brand.ToString() ?? "",
                    IpAddress = cfg?.IpAddress ?? "",
                    Port = cfg?.Port ?? 0,
                    State = status.State,
                    SignalCount = _monitor.Channels.TryGetValue(status.PlcId, out var ch)
                        ? ch.SignalDefs.Count : 0
                });
            }
            RefreshStats();
        }

        private void OnSignalUpdate(SignalUpdate update)
        {
            // 按 SelectedPlcId 过滤
            if (!string.IsNullOrEmpty(_selectedPlcId) && update.PlcId != _selectedPlcId)
                return;

            lock (_signalsLock)
            {
                var existing = _signals.FirstOrDefault(s => s.Id == update.SignalId);
                if (existing != null)
                {
                    existing.Apply(update);
                }
                else
                {
                    // 找到配置信息
                    var cfg = _monitor.Channels.Values
                        .SelectMany(c => c.SignalDefs)
                        .FirstOrDefault(s => s.Id == update.SignalId);

                    if (cfg != null)
                    {
                        var item = new SignalDisplayItem(cfg, update);
                        _signals.Add(item);
                    }
                }
            }

            LastRefreshLabel = $"最后刷新: {DateTime.Now:HH:mm:ss}";
        }

        private void RefreshPlcList()
        {
            _plcList.Clear();
            foreach (var cfg in _monitor.PlcConfigs)
            {
                var ch = _monitor.Channels.TryGetValue(cfg.Id, out var channel) ? channel : null;
                _plcList.Add(new PlcStatusItem
                {
                    Id = cfg.Id,
                    Name = cfg.Name,
                    Brand = cfg.Brand.ToString(),
                    IpAddress = cfg.IpAddress,
                    Port = cfg.Port,
                    State = ch?.State ?? ConnectionState.Disconnected,
                    SignalCount = ch?.SignalDefs.Count ?? 0
                });
            }
            RefreshStats();
            if (string.IsNullOrEmpty(_selectedPlcId) && _plcList.Count > 0)
                _selectedPlcId = _plcList[0].Id;
        }

        private void RefreshSignals()
        {
            lock (_signalsLock)
            {
                var signalDefs = string.IsNullOrEmpty(_selectedPlcId)
                    ? _monitor.Channels.Values.SelectMany(c => c.SignalDefs).ToList()
                    : (_monitor.Channels.TryGetValue(_selectedPlcId, out var ch)
                        ? ch.SignalDefs.ToList()
                        : new System.Collections.Generic.List<SignalData>());

                // 添加缺失的信号项（初始为空值等待数据推送）
                foreach (var def in signalDefs)
                {
                    if (!_signals.Any(s => s.Id == def.Id))
                        _signals.Add(new SignalDisplayItem(def));
                }
            }
            SignalCountLabel = $"{_signals.Count} 信号";
        }

        private void RefreshStats()
        {
            int connected = _plcList.Count(p => p.State == ConnectionState.Connected);
            PlcCountLabel = connected > 0
                ? $"{connected}/{_plcList.Count} PLC 已连接"
                : $"{_plcList.Count} 个 PLC";
            OnPropertyChanged(nameof(AnyConnected));
        }

        public void SelectPlc(string plcId)
        {
            SelectedPlcId = plcId;
            SelectedPlc = _plcList.FirstOrDefault(p => p.Id == plcId);
        }

        public void Dispose()
        {
            _statusSub?.Dispose();
            _signalSub?.Dispose();
            _monitor?.Dispose();
        }
    }

    // ---- 内嵌子类（保持在同一文件以兼容现有引用）----

    public class PlcStatusItem : ViewModelBase
    {
        private ConnectionState _state = ConnectionState.Disconnected;
        private int _retryCount;
        private Brush _statusColor;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int SignalCount { get; set; }
        public int RetryCount
        {
            get => _retryCount;
            set => SetProperty(ref _retryCount, value);
        }

        public ConnectionState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusIcon));
                    StatusColor = _state switch
                    {
                        ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                        ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                        ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                        ConnectionState.Faulted => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                        _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                    };
                }
            }
        }

        public bool IsConnected => _state == ConnectionState.Connected;

        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public string IpPort => $"{IpAddress}:{Port}";

        public string StatusText => _state switch
        {
            ConnectionState.Connected => "已连接",
            ConnectionState.Connecting => "连接中...",
            ConnectionState.Reconnecting => $"重连中(第{_retryCount}次)",
            ConnectionState.Faulted => "故障",
            _ => "离线"
        };

        public string StatusIcon => _state switch
        {
            ConnectionState.Connected => "●",
            ConnectionState.Connecting => "◐",
            ConnectionState.Reconnecting => "◑",
            ConnectionState.Faulted => "○",
            _ => "○"
        };
    }

    public class SignalDisplayItem : ViewModelBase
    {
        private object _value;
        private bool _isChanged;
        private DateTime _lastUpdateTime;
        private string _displayValue;
        private Brush _valueColor;
        private string _lastError;

        public string Id { get; }
        public string Name { get; }
        public string Address { get; }
        public DataTypeEnum DataType { get; }
        public int ArrayLength { get; }
        public string Group { get; }
        public string PlcId { get; }

        public object Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public bool IsChanged
        {
            get => _isChanged;
            set => SetProperty(ref _isChanged, value);
        }

        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }

        public string DisplayValue
        {
            get => _displayValue;
            set => SetProperty(ref _displayValue, value);
        }

        public Brush ValueColor
        {
            get => _valueColor;
            set => SetProperty(ref _valueColor, value);
        }

        public string LastError
        {
            get => _lastError;
            set => SetProperty(ref _lastError, value);
        }

        public string StatusIcon => LastError != null ? "⚠" : "●";
        public string ChangeIcon => IsChanged ? "★" : "";

        /// <summary>从配置创建（初始状态，无数据）</summary>
        public SignalDisplayItem(SignalData def)
        {
            Id = def.Id;
            Name = def.Name;
            Address = def.Address;
            DataType = def.DataType;
            ArrayLength = def.ArrayLength;
            Group = def.Group;
            PlcId = def.PlcId;
            DisplayValue = "---";
            ValueColor = Brushes.White;
            LastUpdateTime = DateTime.MinValue;
        }

        /// <summary>从 SignalUpdate 创建或更新</summary>
        public SignalDisplayItem(SignalData def, SignalUpdate update) : this(def)
        {
            Apply(update);
        }

        public void Apply(SignalUpdate update)
        {
            LastUpdateTime = update.Timestamp;

            if (update.Value.IsOk)
            {
                var previousValue = Value;
                Value = update.Value.Value;
                IsChanged = !Equals(Value, previousValue);
                DisplayValue = FormatValue(update.Value.Value, DataType);
                ValueColor = Brushes.White;
                LastError = null;
            }
            else
            {
                IsChanged = false;
                LastError = update.Value.Error;
                DisplayValue = $"Err: {update.Value.Error}";
                ValueColor = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            }

            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(ChangeIcon));
        }

        private static string FormatValue(object value, DataTypeEnum dataType)
        {
            if (value == null) return "null";

            return dataType switch
            {
                DataTypeEnum.Bool => (bool)value ? "ON" : "OFF",
                DataTypeEnum.BoolArray when value is bool[] barr => "[" + string.Join(",", barr) + "]",
                DataTypeEnum.ShortArray when value is short[] sarr => "[" + string.Join(",", sarr) + "]",
                DataTypeEnum.IntArray when value is int[] iarr => "[" + string.Join(",", iarr) + "]",
                _ => value.ToString()
            };
        }
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build
```

可能出现的错误:
- `PlcConnectionFactory.Create` 需要导入
- `ObserveOnDispatcher()` 需要 `System.Reactive` 的 `DispatcherScheduler`
- `Observable.Merge` 需要 `System.Reactive.Linq`

上述都在 Step 1 代码中通过 `using System.Reactive.Linq` 处理。如果编译报错，检查 `using` 指令。

- [ ] **Step 3: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "refactor: rewrite ViewModels with Rx subscriptions, Result<T>, ConnectionState"
```

---

### Task 13: 更新 MainWindow 和相关 View

**Files:**
- Modify: `MainWindow.xaml.cs`
- Modify: `View/PLCConnectionView.xaml.cs`
- Modify: `View/PLCConnectionView.xaml`
- Modify: `View/SignalMonitorView.xaml`

- [ ] **Step 1: 重写 MainWindow.xaml.cs**

用以下内容替换 `MainWindow.xaml.cs`：

```csharp
using System;
using System.Windows;
using WpfApp1.PLCHandler;
using WpfApp1.ViewModels;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            var configDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "Config"
            );
            var configService = new PlcConfigService(configDir);
            var monitor = new PlcMonitor(configService);
            _vm = new MainViewModel(monitor);
            DataContext = _vm;

            // 默认显示 PLC连接视图
            contentArea.Content = new View.PLCConnectionView { DataContext = _vm };

            Closed += (s, e) => _vm.Dispose();
        }

        private void NavRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (contentArea == null) return;

            var tag = (sender as System.Windows.Controls.RadioButton)?.Tag as string;
            if (tag == "SignalMonitor")
            {
                _vm.SelectedView = "SignalMonitor";
                contentArea.Content = new View.SignalMonitorView { DataContext = _vm };
            }
            else
            {
                _vm.SelectedView = "PLCConnection";
                contentArea.Content = new View.PLCConnectionView { DataContext = _vm };
            }
        }
    }
}
```

- [ ] **Step 2: 更新 PLCConnectionView.xaml.cs**

`gridPlc_SelectedItemChanged` 中 `PlcStatusItem` 的引用保持不变（类仍在 MainViewModel.cs 中），但需要检查命名空间。确认 `WpfApp1.ViewModels.PlcStatusItem` 即可。

- [ ] **Step 3: 更新 View XAML 绑定**

`PLCConnectionView.xaml` 中的 `FieldName="IpPort"` 保持不变（属性名未变）。`StatusIcon` 现在有 PropertyChanged 通知（通过 State 属性触发）。

无需修改 XAML 文件，绑定属性名与新旧模型一致。

- [ ] **Step 4: 编译并运行测试**

```bash
dotnet build
```

```bash
# 启动应用
dotnet run
```

测试要点：
1. 应用启动不崩溃
2. 左侧面板显示 3 个 PLC（2 Omron + 1 Siemens），全部初始状态为 "离线"
3. 由于 127.0.0.1 无真 PLC，应看到: 离线 → 连接中... → 重连中(第1次) → ... → 故障
4. 右侧详情面板绑定正常

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "refactor: update MainWindow to instantiate PlcMonitor via DI-like pattern"
```

---

### Task 14: 清理旧文件 + 将 PlcConnectionFactory.Create 改为静态

**Files:**
- Delete: `PLCHandler/PlcHandler.cs`
- Delete: `PLCHandler/PlcConnectionPool.cs`
- Delete: `PLCHandler/PollingService.cs`
- Modify: `PLCHandler/PlcConnectionFactory.cs`（Create 改为 static）
- 保留: `PLCHandler/PlcConfigService.cs`
- 保留: `PLCHandler/Models/*.cs`
- 保留: `PLCHandler/Core/*.cs`
- 保留: `PLCHandler/Channel/*.cs`
- 保留: `PLCHandler/Connections/*.cs`

- [ ] **Step 1: 修改 PlcConnectionFactory.Create 为静态方法**

编辑 `PLCHandler/PlcConnectionFactory.cs`，将 `Create` 方法改为 `public static`：

```csharp
using System;
using WpfApp1.PLCHandler.Models;

namespace WpfApp1.PLCHandler
{
    public static class PlcConnectionFactory
    {
        public static IPlcConnection Create(PlcConnectionOptions options)
        {
            return options.Brand switch
            {
                PLCBrand.Siemens => new SiemensConnection(options),
                PLCBrand.Omron => new OmronConnection(options),
                PLCBrand.Mitsubishi => new MitsubishiConnection(options),
                PLCBrand.ModbusTcp => new ModbusConnection(options),
                _ => throw new ArgumentException($"Unsupported PLC brand: {options.Brand}")
            };
        }
    }
}
```

- [ ] **Step 2: 删除旧文件**

```bash
git rm PLCHandler/PlcHandler.cs
git rm PLCHandler/PlcConnectionPool.cs
git rm PLCHandler/PollingService.cs
```

- [ ] **Step 3: 编译验证**

```bash
dotnet build
```

如果 PlcConnectionFactory 被 PlcMonitor 引用（见 Task 10），则不能删除。检查后决定：

- 如果 PlcConnectionFactory.Create 只在 PlcMonitor 中用，可以保留工厂文件
- 如果 Create 方法已内联到 PlcMonitor，删除工厂文件并修复引用

编译必须 0 errors。

- [ ] **Step 4: 最终运行测试**

```bash
dotnet run
```

确认：
- 无启动崩溃
- 3 个 PLC 在列表中显示
- 状态机正常流转（离线→连接中→重连中→故障）
- 信号监控页切换正常

- [ ] **Step 5: Commit**

```bash
git commit -m "chore: remove legacy PlcHandler, ConnectionPool, PollingService"
```

---

### Task 15: 最终验证和 config 更新

**Files:**
- 无需修改文件

- [ ] **Step 1: 全量编译**

```bash
dotnet build -c Release
```

预期：0 errors, 0 warnings.

- [ ] **Step 2: 运行 smoke test**

```bash
dotnet run
```

验证清单：
- [ ] 窗口正常显示，深色主题
- [ ] 左侧导航 "PLC连接" / "信号监控" 切换正常
- [ ] PLC 列表显示 3 个条目
- [ ] 状态流转：离线 → 连接中... → 重连中 → 故障（由于 127.0.0.1 无真 PLC）
- [ ] Status Bar 显示 "3 个 PLC" 或 "0/3 PLC 已连接"
- [ ] 切换到信号监控页，显示信号列表（0 信号或有信号但值为 Err）
- [ ] 关闭窗口不崩溃

- [ ] **Step 3: Config 文件清理**

当前 config 中所有 PLC 设为 `127.0.0.1`。这是正确的 demo 配置——演示本地无 PLC 时的完整状态链。保留不改。

- [ ] **Step 4: Final commit**

```bash
git commit -m "chore: final verification — build passes, demo state chain works"
```
