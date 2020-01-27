$ErrorActionPreference = "Stop"

function DownloadFile($url, $outputPath) {
	Write-Host "Attempt to download to $outputPath"

	# If the file has been downloaded don't download again. An empty file implies a failed download
	if(Test-Path $outputPath) {
		$file = Get-ChildItem $outputPath

		if($file.Length -gt 0) {
			Write-Host "$outputPath is already downloaded"
			return;
		}
	}

	try {
		# Create placeholder so directory exists
		New-Item -Type File $OutputPath -Force | Out-Null

		# Attempt to download.  If fails, placeholder remains so msbuild won't complain
		Invoke-WebRequest $url -OutFile $OutputPath | Out-Null

		Write-Host "Downloaded $OutputPath"
	} catch {
		Write-Error "Failed to download '$url'. $($Error[0])"
	}
}

$address = "https://portability.blob.core.windows.net/catalog/catalog.bin"

DownloadFile "$address" "$PSScriptRoot\.data\catalog.bin"
