# MSLX 服务器回档插件

为 MSLX 开服器提供一键回档功能的插件。

## 项目声明

本插件基于 MSLX 官方提供的示例插件进行二次开发，感谢 MSLX 团队提供的优秀开发框架和示例代码。

## 功能特性

- 📋 **服务器信息展示** - 实时显示服务器名称、路径、运行状态等信息
- 📁 **备份文件管理** - 自动扫描并展示备份目录中的所有备份压缩包
- ⚙️ **存档路径配置** - 支持自定义存档路径设置
- 📢 **回档公告** - 支持发送回档公告通知玩家
- ⏱️ **倒计时确认** - 回档前的确认倒计时机制
- 🔄 **一键回档** - 快速恢复服务器存档到指定备份版本

## 界面预览

### 主界面

![插件主界面](assets/rollback-main.png)

### 备份文件列表

![备份文件列表](assets/rollback-backups.png)

## 技术栈

- **后端**: .NET 10 + C#
- **前端**: Vue 3 + TypeScript + TDesign Vue Next
- **插件框架**: MSLX SDK

## 快速开始

### 方式一：下载预编译版本

推荐直接使用已经编译好的插件：

1. 访问 [GitHub Releases](https://github.com/TNT-12M/mslx-plugin-rollback/releases) 页面
2. 下载最新版本的 `mslx-plugin-rollback.dll` 文件
3. 将下载的 DLL 文件放入 MSLX 插件目录
4. 重启 MSLX 服务

### 方式二：自行编译

如果你想从源代码编译：

1. 克隆仓库：
```bash
git clone https://github.com/TNT-12M/mslx-plugin-rollback.git
```

2. 进入项目目录并编译：
```bash
cd mslx-plugin-rollback
dotnet build --configuration Release
```

3. 编译产物位置：
   - Windows: `bin\Release\net10.0\mslx-plugin-rollback.dll`
   - Linux/Mac: `bin/Release/net10.0/mslx-plugin-rollback.dll`

4. 将编译好的 DLL 文件复制到 MSLX 插件目录

5. 重启 MSLX 服务

## 使用方法

1. 在 MSLX 管理中心左侧菜单中点击「服务器回档」
2. 查看服务器状态和备份列表
3. 选择要恢复的备份文件
4. 设置回档公告（可选）和倒计时
5. 点击「执行回档」完成操作

## 项目结构

```
├── Controllers/           # 后端 API 控制器
│   └── RollbackController.cs
├── Frontend/              # 前端代码
│   ├── src/
│   │   ├── views/
│   │   │   └── RollbackPage.vue
│   │   └── pluginEntry.ts
│   └── package.json
├── assets/                # 资源文件
│   ├── rollback-main.png
│   └── rollback-backups.png
├── MSLXPluginEntry.cs     # 插件入口
└── README.md
```

## 许可证

MIT License
