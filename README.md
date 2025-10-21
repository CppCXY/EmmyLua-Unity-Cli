<div align="center">

# 🚀 EmmyLua-Unity-Cli

**为 Unity Lua 框架自动生成高质量的 EmmyLua 类型定义**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/CppCXY/EmmyLua-Unity-Cli)

</div>

---

## 📖 简介

EmmyLua-Unity-Cli 是一个强大的命令行工具，用于从 Unity C# 项目中自动生成 EmmyLua 类型定义文件。它通过分析 C# 代码，为 Lua 开发提供智能提示、类型检查和代码补全功能，大幅提升开发效率。

### ✨ 核心特性

| 特性 | 说明 |
|------|------|
| 🎯 **XLua 支持** | 完整支持 XLua 框架的类型导出和标记 |
| 🔧 **ToLua 支持** | 完整支持 ToLua 框架的类型导出和标记 |
| 📦 **泛型合并** | 智能合并泛型类型实例，减少冗余定义 |
| 🔍 **类型追踪** | 自动追踪未导出的引用类型，生成完整类型别名 |
| 📝 **委托和事件** | 使用 `@alias` 准确表示委托和事件类型 |
| 🎲 **枚举值** | 导出实际枚举常量值，而非递增计数器 |
| ⚡ **高性能** | 基于 Roslyn 编译器 API，分析速度快且准确 |
| 🌐 **多项目支持** | 支持多项目解决方案，自动去重 |

---

## 🚀 快速开始

### 📋 系统要求

- **.NET 8.0 SDK**
- **MSBuild** (随.NET SDK 安装)

### 📦 安装

```bash
# 克隆仓库
git clone https://github.com/CppCXY/EmmyLua-Unity-LS.git
cd EmmyLua-Unity-LS

# 编译项目
dotnet build -c Release

# 发布可执行文件（可选）
dotnet publish -c Release -o ./publish
```

### 💻 使用示例

#### XLua 项目

```bash
# 基本用法
unity --solution YourProject.sln --bind XLua --output ./lua_definitions

# 指定构建配置
unity --solution YourProject.sln --bind XLua --output ./output --properties "Configuration=Release"
```

#### ToLua 项目

```bash
# 基本用法
unity --solution YourProject.sln --bind ToLua --output ./lua_definitions

# 多个 MSBuild 属性
unity --solution YourProject.sln --bind ToLua --output ./output \
      --properties "Configuration=Release" \
      --properties "Platform=AnyCPU"
```

---

## 📚 命令行参数

| 参数 | 简写 | 必需 | 说明 | 示例 |
|------|------|------|------|------|
| `--solution` | `-s` | ✅ | Unity 解决方案文件路径 (.sln) | `YourProject.sln` |
| `--bind` | `-b` | ✅ | Lua 绑定框架类型 | `XLua`, `ToLua` |
| `--output` | `-o` | ✅ | 类型定义文件输出目录 | `./lua_definitions` |
| `--properties` | `-p` | ❌ | MSBuild 构建属性 | `Configuration=Release` |
| `--export` | `-e` | ❌ | 导出格式 (保留参数) | `Json`, `Lua` |

---

## 🎯 功能详解

### 🔹 智能泛型合并

自动合并同一泛型类型的多个实例，生成统一的泛型定义：

```lua
-- 合并前：多个具体实例
---@class System.Collections.Generic.List<System.Int32>
---@class System.Collections.Generic.List<System.String>

-- 合并后：统一泛型定义
---@class System.Collections.Generic.List<T>
```

### 🔹 委托和事件支持

使用 EmmyLua 的 `@alias` 语法准确表示委托类型：

```lua
---@alias UnityAction fun()
---@alias UnityAction<T> fun(arg0: T)

---@class UnityEngine.UI.Button
---@field onClick UnityEngine.UI.Button.ButtonClickedEvent
```

### 🔹 枚举实际值导出

导出 C# 枚举的真实常量值：

```lua
---@enum KeyCode
local KeyCode = {
    None = 0,
    Backspace = 8,
    Tab = 9,
    Return = 13,
    Escape = 27,
    Space = 32,
}
```

### 🔹 未导出类型追踪

自动生成 `xlua_noexport_types.lua` 或 `tolua_noexport_types.lua`，包含所有被引用但未导出的类型别名：

```lua
---@alias UnityEngine.Component any
---@alias UnityEngine.Transform any
---@alias UnityEngine.GameObject any
```

### 🔹 默认类型导出

自动包含常用泛型类型：
- `System.Collections.Generic.List<T>`
- `System.Collections.Generic.Dictionary<TKey, TValue>`

自动生成全局函数：
- `typeof(type)` - XLua/ToLua 的类型获取函数

---

## 🏗️ 项目结构

```
EmmyLua-Unity-LS/
├── EmmyLua.Unity.Cli/          # 主命令行工具项目
│   ├── Generator/              # 代码生成器核心
│   │   ├── XLua/              # XLua 框架支持
│   │   │   ├── XLuaClassFinder.cs
│   │   │   └── XLuaDumper.cs
│   │   ├── ToLua/             # ToLua 框架支持
│   │   │   ├── ToLuaClassFinder.cs
│   │   │   └── ToLuaDumper.cs
│   │   ├── CSharpAnalyzer.cs  # Roslyn 代码分析器
│   │   ├── GenericTypeManager.cs  # 泛型合并管理
│   │   ├── TypeReferenceTracker.cs  # 类型引用追踪
│   │   └── LuaAnnotationFormatter.cs  # EmmyLua 注解格式化
│   └── Program.cs             # 程序入口
├── build/                      # 构建输出目录
├── README.md                   # 本文档
└── LICENSE                     # MIT 许可证
```

---

## 🔧 开发指南

### 编译项目

```bash
# Debug 模式
dotnet build

# Release 模式
dotnet build -c Release
```

### 运行测试

```bash
dotnet test
```

### 代码格式化

```bash
dotnet format
```

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 贡献流程

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

---

## 📄 开源协议

本项目基于 [MIT License](LICENSE) 开源。

---

## 🙏 致谢

- [EmmyLua](https://emmylua.github.io/) - 优秀的 Lua 语言服务器
- [Roslyn](https://github.com/dotnet/roslyn) - .NET 编译器平台
- [XLua](https://github.com/Tencent/xLua) - Unity Lua 解决方案
- [ToLua](https://github.com/topameng/tolua) - Unity Lua 框架

---

<div align="center">

**如果这个项目对你有帮助，请给它一个 ⭐️**

</div>

