Param(
    [Parameter(Mandatory=$true)]
    [string]
    $newVersionName,
    [Parameter(Mandatory=$true)]
    [string]
    $signingKeyPass,
    [string]
    $outputDirectory = (Join-Path $PSScriptRoot 'artifacts')
)

Function ChangeVersion {
    Param(
        [Parameter(Mandatory=$true)]
        [string]
        $newVersionName
    )

    $csprojPath = Join-Path $PSScriptRoot 'RacingCarsControllerAndroid/RacingCarsControllerAndroid.csproj'
    $projectContent = Get-Content -Path $csprojPath -Raw
    $versionMatch = [regex]::Match($projectContent, '<ApplicationVersion>(\d+)</ApplicationVersion>')
    if (-not $versionMatch.Success) {
        throw 'Could not find ApplicationVersion in the Android project.'
    }

    $newVersionCode = ([int]$versionMatch.Groups[1].Value + 1).ToString()
    $projectContent = [regex]::Replace($projectContent, '<ApplicationVersion>\d+</ApplicationVersion>', "<ApplicationVersion>$newVersionCode</ApplicationVersion>")
    $projectContent = [regex]::Replace($projectContent, '<ApplicationDisplayVersion>[^<]+</ApplicationDisplayVersion>', "<ApplicationDisplayVersion>$newVersionName</ApplicationDisplayVersion>")
    Set-Content -Path $csprojPath -Value $projectContent -NoNewline

    Write-Host "New versionName: $newVersionName, new versionCode: $newVersionCode"
}

Function Build {
    Param(
        [Parameter(Mandatory=$true)]
        [string]
        $signingKeyPass
    )

    $projectPath = Join-Path $PSScriptRoot 'RacingCarsControllerAndroid/RacingCarsControllerAndroid.csproj'
    dotnet publish $projectPath -f:net9.0-android -c:Release `
        -p:AndroidPackageFormats=apk `
        "-p:AndroidSigningKeyPass=$signingKeyPass" `
        "-p:AndroidSigningStorePass=$signingKeyPass"
    if ($LASTEXITCODE -ne 0) {
        throw 'Android APK build failed.'
    }
}

Function CopyPublishedApks {
    Param(
        [Parameter(Mandatory=$true)]
        [string]
        $versionName,
        [Parameter(Mandatory=$true)]
        [string]
        $outputDirectory
    )

    $publishDirectory = Join-Path $PSScriptRoot 'RacingCarsControllerAndroid/bin/Release/net9.0-android/publish'
    $appName = 'RacingCarsController'
    $apks = Get-ChildItem -Path $publishDirectory -Filter '*-Signed.apk' -File
    if (-not $apks) {
        throw "No signed APKs were found in $publishDirectory."
    }
    New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null

    foreach ($apk in $apks) {
        $architecture = [IO.Path]::GetFileNameWithoutExtension($apk.Name) -replace '^com\.tmk907\.RacingCarsControllerAndroid-', '' -replace '-Signed$', ''
        $publishedApkName = "$appName-v$versionName-$architecture.apk"
        Copy-Item -Path $apk.FullName -Destination (Join-Path $outputDirectory $publishedApkName)
        Write-Host "$publishedApkName copied to $outputDirectory"
    }
}

ChangeVersion -newVersionName $newVersionName
Build -signingKeyPass $signingKeyPass
CopyPublishedApks -versionName $newVersionName -outputDirectory $outputDirectory