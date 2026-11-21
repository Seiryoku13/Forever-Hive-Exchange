param([switch]$Test)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$bin=Join-Path $root 'bin'
New-Item $bin -ItemType Directory -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('/nologo','/optimize+','/checked+','/utf8output','/r:System.Web.Extensions.dll','/r:System.Data.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.DataVisualization.dll','/r:System.Net.Http.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$sources=@(Get-ChildItem (Join-Path $root 'src/*.cs') | ForEach-Object FullName)
& $csc @refs /target:winexe "/out:$bin/ForeverHiveExchange.exe" @sources
if($LASTEXITCODE){throw 'Desktop build failed'}
if($Test){
  & $csc @refs /target:exe /main:ForeverHive.Tests "/out:$bin/Exchange.Tests.exe" @sources (Join-Path $root 'tests/Tests.cs')
  if($LASTEXITCODE){throw 'Test build failed'}
  & "$bin/Exchange.Tests.exe"
  if($LASTEXITCODE){throw 'Tests failed'}
}
if(Test-Path (Join-Path $root 'installer/Setup.cs')){
  & $csc /nologo /optimize+ /target:winexe /r:System.Windows.Forms.dll "/resource:$bin/ForeverHiveExchange.exe,desktop" "/resource:$root/README.md,guide" "/out:$bin/ForeverHiveExchange-Setup.exe" (Join-Path $root 'installer/Setup.cs')
  if($LASTEXITCODE){throw 'Installer build failed'}
}
