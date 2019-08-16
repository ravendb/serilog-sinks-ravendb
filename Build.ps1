param(
    [String] $majorMinor = "0.0",  # 2.0
    [String] $patch = "0",         # $env:APPVEYOR_BUILD_VERSION
    [String] $customLogger = "",   # C:\Program Files\AppVeyor\BuildAgent\dotnetcore\Appveyor.MSBuildLogger.dll
    [Switch] $notouch,
    [String] $sln                  # e.g serilog-sink-name
)

function Set-AssemblyVersions($informational, $assembly)
{
    (Get-Content assets/CommonAssemblyInfo.cs) |
        ForEach-Object { $_ -replace """1.0.0.0""", """$assembly""" } |
        ForEach-Object { $_ -replace """1.0.0""", """$informational""" } |
        ForEach-Object { $_ -replace """1.1.1.1""", """$($informational).0""" } |
        Set-Content assets/CommonAssemblyInfo.cs
}

function Install-NuGetPackages($solution)
{
    dotnet restore $solution
}

function Invoke-MSBuild($solution, $customLogger)
{
    if ($customLogger)
    {
        dotnet build "$solution" --verbosity minimal --configuration Release -logger:$customLogger
    }
    else
    {
        dotnet build "$solution" --verbosity minimal --configuration Release
    }
}

function Invoke-NuGetPackProj($csproj)
{
    nuget pack $csproj -Symbols -Properties Configuration=Release 
}

function Invoke-NuGetPackSpec($nuspec, $version)
{
    nuget pack $nuspec -Version $version -OutputDirectory ..\..\
}

function Invoke-Tests($slnFile)
{
    dotnet test $slnFile
}

function Invoke-NuGetPack($version)
{
    Get-ChildItem src/**/*.csproj |
        Where-Object { -not ($_.Name -like "*net40*") } |
        ForEach-Object { Invoke-NuGetPackProj $_ }
}

function Invoke-Build($majorMinor, $patch, $customLogger, $notouch, $sln)
{
    $package="$majorMinor.$patch"
    $slnfile = "$sln.sln"

    Write-Output "$sln $package"

    if (-not $notouch)
    {
        $assembly = "$majorMinor.0.0"

        Write-Output "Assembly version will be set to $assembly"
        Set-AssemblyVersions $package $assembly
    }

    Install-NuGetPackages $slnfile
    
    Invoke-MSBuild $slnfile $customLogger

    Invoke-Tests $slnFile

    Invoke-NuGetPack $package
}

$ErrorActionPreference = "Stop"

if (-not $sln)
{
    $slnfull = Get-ChildItem *.sln |
        Where-Object { -not ($_.Name -like "*net40*") } |
        Select-Object -first 1

    $sln = $slnfull.BaseName
}

Invoke-Build $majorMinor $patch $customLogger $notouch $sln
