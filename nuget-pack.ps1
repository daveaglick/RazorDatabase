$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..'
$version = [System.Reflection.Assembly]::LoadFile("$root\RazorDatabase\bin\Release\RazorDatabase.dll").GetName().Version
$versionStr = "{0}.{1}.{2}" -f ($version.Major, $version.Minor, $version.Build)

Write-Host "Setting .nuspec version tag to $versionStr"

$content = (Get-Content $root\RazorDatabase.nuspec) 
$content = $content -replace '\$version\$',$versionStr

$content | Out-File $root\RazorDatabase.compiled.nuspec

& $root\.nuget\NuGet.exe pack $root\RazorDatabase.compiled.nuspec