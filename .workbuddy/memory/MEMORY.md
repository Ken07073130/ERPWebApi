# 项目长期记忆 —— ERPWebApi

## 项目定位

内部 ERP / MES 生产管理系统后端。前端独立仓库，Vue 3 + Element Plus，经 HTTP + CORS 交互。用户本人单独开发，正在上手写代码。

**关键约束（决定所有架构取舍，2026-10-13 用户亲述，已按最新口径修正）**：

- **项目性质：新厂房的新系统，前后端全部新做**（前端 Vue 3 + Element Plus，后端 ASP.NET Core 8）。用户此前用 ASP.NET WebForms 开发过旧 ERP，业务逻辑本人清楚，**无需做业务建模讨论**。
- **旧数据全部丢弃，不做迁移。** 旧库仅作**业务需求文档**（字段里藏着业务规则化石，如「是否含税」「允许超交比例」），通读目的是**防遗漏**而非迁移。旧系统数据库备份建议长期保留备查。
- **新库表结构重新设计，不照搬旧库**——6 条数据约定（自增主键 + 独立业务编号、审计五件套、RowVersion 乐观并发、主数据软删除、decimal 精度、时间存 UTC）**全部可落地**。
- **两厂房的库与主数据均独立**：不需要多租户 / `FactoryId` 隔离，不需要主数据同步机制。老厂房是否也迁到新系统尚未确认。
- **上线方式：分批提前上线**（新厂房数据少、风险可控，无「全员停工切换日」压力）。**不做绞杀者 / 新旧共存**，相关共存复杂度不适用。
- 预期生命周期 **10 年以上**；开发人员 **1 人**。
- 推论一：**重仪式架构（MediatR / 全盘 CQRS / 领域事件）不适用**——其收益来自多人并行的边界仲裁，单人拿不到，成本全额支付。
- 推论二：10 年项目的真实风险在**数据模型约定**而非目录结构；.NET 8 LTS 于 2026-11 结束支持，10 年必然跨多次大版本升级，故**第三方依赖越少越好**（MediatR/AutoMapper 商业授权变更即实例）。
- 推论三：**WebForms → ASP.NET Core 代码复用率 ≈ 0**；最大失败模式是「翻译式重构」（逐行翻成新语法，旧架构问题原样继承）。
- 从 WebForms 过来必改习惯：全链路 async、弃用 Session、DI 生命周期（DbContext 不可 Singleton）、IOptions 配置绑定、Policy 式授权、禁用 `HttpContext.Current`。
- 账号体系全新，登录直接用 `PasswordHasher<T>`（PBKDF2），**不需兼容 MD5 / 首登迁移**；管理员建号给初始密码 + 首登强制改密。
- 基础数据初始化（物料/BOM/客户/供应商）是实打实工作量且常挤压上线时间 → 走 Excel 整理 + 批量导入功能。

## 技术栈与版本（已锁定）

**2026-10-13：目标框架已从 `net8.0` 升级到 `net10.0`**（用户确认）。理由：.NET 8 支持截止 **2026-11-10**（微软官方已核实），距升级时仅 28 天；.NET 10 是 LTS（支持到 2028-11），且本机只装了 SDK 10.0.301，net10 反而无需额外安装。

- ASP.NET Core 10 Web API（`net10.0`），单项目 `ERPWebApi`
- EF Core 10.0.12 + `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12
- `Swashbuckle.AspNetCore` 10.3.0
- `Design` / `Tools` 两包须带 `PrivateAssets="all"`
- NuGet 源为国内镜像 `nuget.azure.cn`
- 关键传递依赖（net10 下）：`Microsoft.Data.SqlClient` 6.1.6、`Microsoft.IdentityModel.Tokens` 8.19.2、`Microsoft.OpenApi` 2.7.5、共 56 个包，**无版本冲突**
- 编译输出目录：`bin/Debug/net10.0/`
- `Swashbuckle` 仍可用（Swagger JSON 实测 200，openapi 3.0.4）。.NET 10 另有第一方 `Microsoft.AspNetCore.OpenApi`，是否替换尚未决定。

## 架构决策

**采用单项目 + 文件夹分层，暂不拆多项目。** 理由：用户正上手写代码，拆项目会打断节奏；单项目下依赖规则退化为约定，这是认下的代价。

- 命名空间：`ERPWebApi.Domain` / `.Application` / `.Infrastructure` / `.Controllers`
- 接口放 `Application/Abstractions`，实现在 `Infrastructure/`
- `Controllers` 不得引用 `Infrastructure`（唯一例外：`Program.cs` 调 DI 扩展方法）
- 规范文档：`docs/单项目分层说明.md`（落地版）、`docs/后端架构与目录结构.md`（多项目目标形态）
- 升级多项目的触发条件：出现第二个业务模块 / 第二个人加入 / 检测到分层被穿透

## 环境事实（省去重复排查）

- 本机 **.NET SDK 10.0.301**（与当前 `net10.0` 目标框架匹配，无额外安装需求）
- **`dotnet ef` CLI 未安装**。若走 CLI，版本需为 **10.0.12**（`dotnet tool install --global dotnet-ef --version 10.0.12`）；若走 VS 的包管理器控制台（PMC），`Tools` 包已装，无需 CLI
- **本机没有完整的 SQL Server 实例**（无 `MSSQL/Binn` 目录，1433 端口未监听）→ `appsettings.Development.json` 里的 `Server=localhost` **目前连不上**。只装了 LocalDB 二进制（SQL Server 2025，17.0.4025.3，位于 `C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe`），且**尚无任何 LocalDB 实例**（`sqllocaldb info MSSQLLocalDB` 报「未找到自动实例」）。两条路：① `Server=(localdb)\MSSQLLocalDB`（首次连接自动创建实例；Dev 够用，但有 10GB/库上限、无 SQL Agent，不适合 10 年期的最终形态）；② 安装 SQL Server Developer Edition（免费，更接近生产形态）
- 已装运行时：`Microsoft.AspNetCore.App` 10.0.9 / `Microsoft.NETCore.App` 10.0.9（比 NuGet 包 10.0.12 低一个 patch，属正常补丁 roll-forward，不影响运行）
- `reg.exe` / `sc.exe` 被本机安全策略拉黑 → 排查服务或注册表需换方式（直接看程序目录、`ls`、`/dev/tcp` 探端口）
- git 仓库已 init（分支 main），但**全局 user.name / user.email 未配置**，尚无首次提交
- Windows 下重定向输出时中文异常消息乱码（`Console.OutputEncoding` 相关）

## 协作约定（重要）

- **用户对未经许可的文件写入敏感。** 说「只讨论 / 不要写代码」时，一个文件都不写，包括记忆日志。
- 用户要自己写业务代码，不要代写。
- 给建议时直接说结论，同时如实说明方案的不利之处，不要说套话。

## 已完成

- 依赖包修正与编译验证（0 警告 0 错误）
- P0 修复：JWT 认证授权注册（启动期强校验 `Jwt:Key`）、git init + `.gitignore`、appsettings 补连接串与 Jwt 节
- 登录验证实现思路讲解（仅讲解，未落码）
- 单项目分层目录已建（`Domain/` `Application/` `Infrastructure/` 下各子目录 + `.gitkeep`，空目录，无 .cs 代码），落地规范见 `docs/单项目分层说明.md`
- 已与用户对齐**施工顺序**：① 装 dotnet-ef + 定 6 条数据约定 → ② 里程碑 1 登录闭环（User 表 + DbContext + 迁移 → 密码哈希封装 → 登录接口只验密码 → 签发 JWT + 受保护接口 curl 验证 → 前端登录页 + CORS）→ ③ 里程碑 2 权限（模型设计 → 权限表 + Policy 授权）。**登录只依赖 User 表，不依赖权限表**；前端排最后。

## 未完成

- DbContext 未注册、CORS 未配置、`Migrations/` 不存在
- 6 个 NuGet 包尚未被代码使用
- `WeatherForecast` / `WeatherForecastController` 模板残留未删
- 工程化配置仍缺：`.editorconfig`、`Directory.Build.props`、`Directory.Packages.props`、`global.json`、`NuGet.config`
