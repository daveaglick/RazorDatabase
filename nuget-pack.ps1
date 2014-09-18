$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..'
$version = [System.Reflection.Assembly]::LoadFile("$root\RazorDatabase\RazorDatabase\bin\Release\RazorDatabase.dll").GetName().Version
$versionStr = "{0}.{1}.{2}" -f ($version.Major, $version.Minor, $version.Build)

Write-Host "Setting .nuspec version tag to $versionStr"

$content = (Get-Content $root\RazorDatabase\RazorDatabase.nuspec) 
$content = $content -replace '\$version\$',$versionStr

$content | Out-File $root\RazorDatabase\RazorDatabase.compiled.nuspec

& $root\RazorDatabase\.nuget\NuGet.exe pack $root\RazorDatabase\RazorDatabase.compiled.nuspec