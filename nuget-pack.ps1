$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..'
$version = [System.Reflection.Assembly]::LoadFile("$root\RazorDatabase\RazorDatabase\bin\Release\RazorDatabase.dll").GetName().Version
$versionStr = "{0}.{1}.{2}" -f ($version.Major, $version.Minor, $version.Build)
& $root\RazorDatabase\.nuget\NuGet.exe pack $root\RazorDatabase\RazorDatabase\RazorDatabase.csproj -Version $versionStr