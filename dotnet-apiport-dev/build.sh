#!/usr/bin/env bash
set -e

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
export HOME=~
export NUGET_PACKAGES=~/.nuget/packages
export NUGET_HTTP_CACHE_PATH=~/.local/share/NuGet/v3-cache

Configuration=Debug

RootDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DotNetSDKPath=$RootDir"/.tools/dotnet/"$DotNetSDKVersion
DotNetExe=$DotNetSDKPath"/dotnet"

BuildApps=true
RunTests=false

usage() {
	echo "Usage: build.sh [-c|--configuration <Debug|Release>] [--downloadCatalog] [--runTests] [--no-build]"
}

downloadCatalog() {
	local isForce=$1
	local catalog=$RootDir"/.data/catalog.bin"
	local data=$(dirname $catalog)

	if [[ ! -e $data ]]; then
		mkdir $data
	fi

	if [[ $isForce == "true" && -e $catalog ]]; then
		echo "Deleting existing catalog"
		rm $catalog
	fi

	if [[ ! -e $catalog ]]; then
		echo "Downloading catalog.bin..."
		curl --output $catalog "https://portability.blob.core.windows.net/catalog/catalog.bin"
	fi
}

installSDK() {
	local dotnetPath=$(command -v dotnet)

	if [[ $dotnetPath != "" ]]; then
		echo "dotnet is found on PATH. using that."
		DotNetExe=$dotnetPath
		return 0
	fi

	if [[ -e $DotNetExe ]]; then
		echo $DotNetExe" exists.  Skipping install..."
		return 0
	fi

	local DotNetToolsPath=$(dirname $DotNetSDKPath)

	if [ ! -d $DotNetToolsPath ]; then
		mkdir -p $DotNetToolsPath
	fi

	curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --install-dir $DotNetSDKPath
}

tryGetTargetFramework() {
	local file=$1
	local targetFramework=$(awk -F: '/<TargetFramework(s)?>.*netcoreapp[1-9]\.[0-9].*<\/TargetFramework(s)?>/ { print $0 }' $file | sed 's/.*\(netcoreapp[1-9]\.[0-9]\).*/\1/' | tail -n 1)
	echo $targetFramework
}

build() {
	echo "Building ApiPort... Configuration: ["$Configuration"]"

	pushd src/ApiPort/ApiPort >/dev/null

	local targetFramework=$(tryGetTargetFramework ApiPort.props)
	echo "Building for: $targetFramework"

	$DotNetExe build ApiPort.csproj -f $targetFramework -c $Configuration
	$DotNetExe build ApiPort.Offline.csproj -f $targetFramework -c $Configuration
	popd >/dev/null
}

runTest() {
	ls $1/*.csproj | while read file; do
		local targetFramework=$(tryGetTargetFramework $file)

		if [[ $targetFramework == "" ]]; then
			echo "Skipping "$file
			echo "--- Desktop .NET Framework testing is not currently supported on Unix."
		else
			echo "Testing "$file
			$DotNetExe test $file -c $Configuration --logger trx --framework $targetFramework --results-directory $2
		fi
	done
}

findAndRunTests() {
	local testResultsDirectory=$COMMON_TESTRESULTSDIRECTORY

	if [[ $testResultsDirectory == "" ]]; then
		testResultsDirectory=$RootDir/TestResults
		echo "Results directory not specified, using $testResultsDirectory."
	else
		echo "Using common one set by build agent."
	fi

	find tests/ -type d -name "*\.Tests" | while read file; do
		runTest $file $testResultsDirectory
	done
}

while [[ $# -gt 0 ]]; do
	option="$(echo $1 | awk '{print tolower($0)}')"
	case "$option" in
	"-?" | "--help")
		usage
		exit 1
		;;
	"-c" | "--configuration")
		Configuration="$2"
		shift 2
		;;
	"--runtests")
		RunTests=true
		shift
		;;
	"--no-build")
		BuildApps=false
		shift
		;;
	"--downloadcatalog")
		downloadCatalog "true"
		exit 0
		;;
	*)
		echo "Unknown option: "$option
		usage
		exit 1
		;;
	esac
done

# Enable insensitive case-matching
shopt -s nocasematch

if [[ "$Configuration" != "Debug" && "$Configuration" != "Release" ]]; then
	echo "ERROR: Supported configuration types are Debug or Release.  Invalid configuration: "$Configuration
	usage
	exit 3
fi

shopt -u nocasematch

installSDK

if [[ ! -e $DotNetExe ]]; then
	echo "ERROR: It should have been installed from build/dotnet-install.sh"
	exit 2
fi

if [[ $BuildApps == "true" ]]; then
	downloadCatalog
	build
fi

if [[ $RunTests == "true" ]]; then
	if [[$BuildApps != "true" ]]; then
		echo "WARNING: Not building applications because --no-build is set."
	fi

	findAndRunTests
fi

echo "Finished!"
