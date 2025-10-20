# EmmyLua-Unity-LS

EmmyLua Unity 插件命令行工具

## 功能特性

- ✅ 支持 XLua 框架的 EmmyLua 定义生成
- ✅ 支持 ToLua 框架的 EmmyLua 定义生成
- 🚧 支持 Puerts 框架（计划中）

## 使用方法

### 基本命令

```bash
# XLua 项目
unity --solution YourProject.sln --bind XLua --output ./lua_definitions

# ToLua 项目
unity --solution YourProject.sln --bind ToLua --output ./lua_definitions

# 带 MSBuild 属性
unity --solution YourProject.sln --bind XLua --output ./output --properties "Configuration=Release"
```

### 命令行参数

- `-s, --solution` (必需): 解决方案文件路径 (.sln)
- `-b, --bind` (必需): 绑定类型 (XLua, ToLua, Puerts)
- `-o, --output` (必需): 输出路径
- `-p, --properties` (可选): MSBuild 属性 (格式: key=value)
- `-e, --export` (可选): 导出类型 (Json, Lua)

## 编译

1. 确认自己的环境支持 .NET 8

2. 使用 Rider 或 VS 打开项目工程即可编译

```bash
dotnet build
```

## 文档

- [ToLua 使用指南](TOLUA_GUIDE.md)
- [代码重构说明](REFACTORING.md)

## LICENSE

[MIT](LICENSE)

