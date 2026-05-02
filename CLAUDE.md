# ZenergyBFSI 项目

蓝膜外观检测上位机系统，基于 WPF (.NET Framework 4.8) 开发。

## 项目结构

- `Service/` - 业务服务（AutoRun、DashboardService、MomHandler 等）
- `View/` - 视图层（WPF 页面和控件）
- `Model/` - 数据模型
- `MOM/` - MOM 通信模块

## gstack

本项目配置使用 gstack 进行网页浏览。

**重要**：所有网页浏览任务必须使用 `/browse` 技能，禁止使用 `mcp__claude-in-chrome__*` 工具。

### 可用技能

- `/office-hours` - YC 风格技术咨询
- `/plan-ceo-review` - CEO/创始人模式计划审查
- `/plan-eng-review` - 工程计划审查
- `/plan-design-review` - 设计计划审查
- `/design-consultation` - 设计咨询
- `/design-shotgun` - 多 AI 设计变体生成
- `/design-html` - 设计定稿生成 HTML/CSS
- `/design-review` - 设计审查
- `/review` - PR 代码审查
- `/ship` - 交付工作流
- `/land-and-deploy` - 合并部署工作流
- `/canary` - 金丝雀监控
- `/benchmark` - 性能回归检测
- `/browse` - 网页浏览和测试
- `/connect-chrome` - 连接 Chrome 浏览器
- `/qa` - QA 测试和修复
- `/qa-only` - QA 测试报告
- `/setup-browser-cookies` - 导入浏览器 cookies
- `/setup-deploy` - 配置部署设置
- `/setup-gbrain` - 设置 gbrain
- `/retro` - 每周工程回顾
- `/investigate` - 问题调查
- `/document-release` - 文档更新
- `/codex` - OpenAI Codex CLI 封装
- `/cso` - 首席安全官模式
- `/autoplan` - 自动审查管道
- `/plan-devex-review` - 开发者体验计划审查
- `/devex-review` - 开发者体验审查
- `/careful` - 破坏性命令安全警告
- `/freeze` - 限制文件编辑范围
- `/guard` - 完整安全模式
- `/unfreeze` - 解除编辑限制
- `/gstack-upgrade` - 升级 gstack
- `/learn` - 学习模式

## 开发说明

- 使用 DevExpress 组件库
- 使用 CommunityToolkit.Mvvm 进行 MVVM 模式开发
- 使用 Dapper 进行数据库访问
- SQLite 本地数据库，SQL Server 远程数据库
