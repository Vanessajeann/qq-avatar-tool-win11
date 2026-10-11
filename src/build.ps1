$ErrorActionPreference = 'Stop'
$sourceDir = $PSScriptRoot
$deliverableDir = Join-Path (Split-Path -Parent $sourceDir) 'bin'
New-Item -ItemType Directory -Path $deliverableDir -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '找不到 Windows 自带的 .NET Framework C# 编译器。' }
$files = @('Protocol.cs','Images.cs','LegacyKey.cs','QQNTBridge.cs','App.cs','Tests.cs') | ForEach-Object { Join-Path $sourceDir $_ }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/win32manifest:$sourceDir\app.manifest" "/resource:$sourceDir\testdata\test.gif,test.gif" "/resource:$sourceDir\testdata\partial.gif,partial.gif" "/out:$deliverableDir\QQAvatarTool.exe" $files
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Output (Join-Path $deliverableDir 'QQAvatarTool.exe')
