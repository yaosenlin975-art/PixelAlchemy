// 职责：为 ProtocolExportTool 生成的 int16/uint16 提供到 System.Int16/System.UInt16 的类型别名。
// Responsibility: Provide type aliases mapping int16/uint16 (emitted by ProtocolExportTool) to System.Int16/System.UInt16.
// 导出工具未将 .proto 的 int16/uint16 映射为 C# 的 short/ushort，本文件补全该映射，
// 使 Generate/NetworkProtocol/OuterMessage.cs 中的 int16/uint16 字段可编译且被 MemoryPack 原生支持。
// The export tool does not map .proto int16/uint16 to C# short/ushort; this file completes that mapping
// so int16/uint16 fields in Generate/NetworkProtocol/OuterMessage.cs compile and are natively supported by MemoryPack.
global using int16 = System.Int16;
global using uint16 = System.UInt16;
