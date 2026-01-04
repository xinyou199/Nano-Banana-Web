# LhzyAI 绘图平台（基于 Nano-Banana）

本仓库实现了一个基于 Nano-Banana 的绘图平台网站（项目名：LhzyAI），支持微信小程序扫码登录、微信支付、后台任务处理和图片处理队列。该项目以 C# (.NET) 实现，包含前后端控制器、业务服务、任务队列与 R2 对象存储支持。

**核心功能**
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/055c3246-6ef3-4b32-924f-ed0ee0f49af1" />
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/a5a88735-c186-4e41-a51c-45f62515cd48" />
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/371c1841-1dc7-4473-af40-0e80e7affded" />
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/b9b7e61e-1635-433b-a1c3-481804cd9b82" />
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/15a7df86-00e0-4b6b-93d4-442c04d6532d" />
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/794eb2e8-c4cc-4a34-865b-a6fdc23f56a2" />
<img width="2550" height="1243" alt="image" src="https://github.com/user-attachments/assets/ad4da80e-2cfd-489f-9503-0a094e6f396a" />

...
**许可证**
- 本仓库包含 `LICENSE.txt`，请查看该文件以确认开源许可证类型与约束。

部署（编译发布并运行）

下面给出两种常见方式：依赖框架（Framework-dependent）和自包含/脱离框架（Self-contained）。示例基于 .NET 8，目标项目示例为 `GoogleAI`，请根据实际项目名调整命令中的路径与输出目录。

1) 依赖框架（需要目标机器已安装 .NET 8 运行时）

Windows (示例):

```powershell
cd GoogleAI
dotnet publish -c Release -r win-x64 --self-contained false -o ../publish/win-x64
# 运行
dotnet ..\publish\win-x64\GoogleAI.dll
```

Linux (示例):

```bash
cd GoogleAI
dotnet publish -c Release -r linux-x64 --self-contained false -o ../publish/linux-x64
# 运行
dotnet ../publish/linux-x64/GoogleAI.dll
```

2) 自包含（脱离框架）- 适用于机器未安装 .NET 的场景

Windows 单文件自包含：

```powershell
cd GoogleAI
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true -o ../publish/win-x64
# 运行（在 Windows 上直接双击或在控制台中运行）
..\publish\win-x64\GoogleAI.exe
```

Linux 单文件自包含：

```bash
cd GoogleAI
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true -o ../publish/linux-x64
# 赋予可执行权限并运行
chmod +x ../publish/linux-x64/GoogleAI
../publish/linux-x64/GoogleAI
```

常用 Runtime Identifiers (RID)：`win-x64`, `win-x86`, `linux-x64`, `linux-arm64` 等。RID 列表见官方文档。

提示与注意事项：
- 如果使用单文件发布（`PublishSingleFile=true`）且开启裁剪（`PublishTrimmed=true`），请充分测试，裁剪可能会移除反射或动态加载依赖。
- 可通过 `-r <RID>` 指定目标平台，多目标平台请为每个平台分别发布。
- 推荐将生产配置放在环境变量或 `appsettings.Production.json` 中，运行时通过 `ASPNETCORE_ENVIRONMENT=Production` 指定环境。
- 在 Linux 上要把服务放后台运行可使用 `nohup`、`systemd` 或容器化部署。

示例：在 Linux 上使用 systemd 启动（简要示例，不是完整 unit 文件）：

```ini
[Unit]
Description=LhzyAI Service

[Service]
WorkingDirectory=/path/to/publish/linux-x64
ExecStart=/path/to/publish/linux-x64/GoogleAI
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

完成后，发布目录 `publish/<RID>` 中会包含可部署的可执行文件及运行所需资源，直接在目标主机控制台启动即可。


# 默认登陆账号密码
账号：admin  
密码：admin123


# 参考项目
https://github.com/RHZHZ/nano-banana-web UI布局部分参考该开源项目
