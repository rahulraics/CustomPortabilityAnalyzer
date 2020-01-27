﻿[CmdletBinding()] # Needed to support -Verbose
param(
    [Parameter(Position = 0, Mandatory=$true)]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration,

    [Parameter(Position = 1, Mandatory=$true)]
    [ValidateSet("AnyCPU", "x86", "x64")]
    [string]$Platform,

    [Parameter(Position = 2)]
    [ValidateSet("quiet", "minimal", "normal", "diagnostic")]
    [string]$Verbosity = "normal",

    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Invoke-Tests() {
    Write-Host "Running tests"

    $testFolder = $(Resolve-Path $([IO.Path]::Combine($root, "tests"))).Path
    $testResults = [IO.Path]::Combine($root, "TestResults")

    if (!(Test-Path $testFolder)) {
        Write-Error "Could not find test folder [$testFolder]"
        return -1
    }

    if (!(Test-Path $testResults)) {
        Write-Host "Creating $testResults folder..."
        New-Item $testResults -ItemType Directory | Out-Null
    }

    dotnet --version

    foreach ($test in $(Get-ChildItem $testFolder | ? { $_.PsIsContainer })) {
        $csprojs = Get-ChildItem $test.FullName -Recurse | ? { $_.Extension -eq ".csproj" }
        foreach ($proj in $csprojs) {
            Write-Host "Testing $($proj.Name). Output: $trx"

            dotnet test "$($proj.FullName)" --configuration $Configuration --logger "trx" --no-build
        }
    }
}

function Set-DevEnvironment {
    Write-Host "Getting msbuild"
    $msbuild = "msbuild"

    if (Get-Command $msbuild -ErrorAction SilentlyContinue)
    {
        Write-Host "$msbuild is already available"
        return
    }

    $microsoftVisualStudio = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2017"

    if (Test-Path $microsoftVisualStudio) {
        $installations = Get-ChildItem $microsoftVisualStudio | ? { $_.PsIsContainer }

        foreach ($installation in $installations) {
            $path = Join-Path $installation.FullName "Common7\Tools\"
            Write-Host $path
            if (Test-Path $path) {
                $commonToolsPath = $path
                break
            }
        }
    } else {
        Write-Error "Could not locate: $microsoftVisualStudio. Pass path to $VsDevCmdBat using parameter -VsDevCmdPath."
    }

    if ([string]::IsNullOrEmpty($commonToolsPath)) {
        Write-Error "Could not find Common Tools for Visual Studio"
    }

    $devEnv = Join-Path $commonToolsPath "VSDevCmd.bat"

    if (!(Test-Path $devEnv)) {
        Write-Error "Could not find VsDevCmd.bat"
    }

    $output = cmd /c "`"$devEnv`" & set"

    foreach ($line in $output)
    {
        if ($line -match "(?<key>.*?)=(?<value>.*)") {
            $key = $matches["key"]
            $value = $matches["value"]

            Write-Verbose("$key=$value")
            Set-Item "ENV:\$key" -Value "$value" -Force
        }
    }

    if (Get-Command $msbuild -ErrorAction SilentlyContinue)
    {
        Write-Host "Added $msbuild to path"
        return
    }

    Write-Error "Could not find $msbuild"
}

Set-DevEnvironment

# Show the MSBuild version for failure investigations
msbuild /version

$msbuild_version = (msbuild /version /nologo | Select-String -pattern '(?<major>[0-9]+)\.(?<minor>[0-9]+)') 
$binarylog_compat = $false

# check if msbuild_version is not null
if ($msbuild_version){
    $msbuild_version = $msbuild_version.Matches[0].Groups

    # check if msbuild_version is greater than or equal to 15.3
    # binary logging was added in 15.3
    if([int]$msbuild_version['major'].Value -ge 15 -and [int]$msbuild_version['minor'].Value -ge 3){
        $binarylog_compat = $true
    }
}

$binFolder = [IO.Path]::Combine("bin", $Configuration)

New-Item $binFolder -ItemType Directory -ErrorAction SilentlyContinue | Out-Null

& "$root\init.ps1"

# PortabilityTools.sln understands "Any CPU" not "AnyCPU"
$PlatformToUse = $Platform

if ($Platform -eq "AnyCPU") {
    $PlatformToUse = "Any CPU"
}
$binarylog_switch = "/bl:$binFolder\msbuild.binlog"
if (!$binarylog_compat) {
    $binarylog_switch = ""
}

Push-Location $root

& msbuild PortabilityTools.sln "/t:restore;build;pack" /p:Configuration=$Configuration /p:Platform="$PlatformToUse" /nologo /m:1 /v:m /nr:false "$binarylog_switch"

Pop-Location

if ($RunTests) {
    Invoke-Tests
}
