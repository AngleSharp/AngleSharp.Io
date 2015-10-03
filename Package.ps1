$dest = "Nuget\lib\net45"
$spec = "Nuget\AngleSharp.Io.nuspec"
New-Item $dest -type directory -Force
Copy-Item "AngleSharp.Io\bin\Release\AngleSharp.Io.dll" $dest
Copy-Item "AngleSharp.Io\bin\Release\AngleSharp.Io.xml" $dest
$file = $dest + "\AngleSharp.Io.dll"
$ver = (Get-Item $file).VersionInfo.FileVersion
$file = "Nuget\AngleSharp.Io." + $ver + ".nupkg"
$repl = '<version>' + $ver + '</version>'
(Get-Content $spec) | 
    Foreach-Object { $_ -replace "<version>([0-9\.]+)</version>", $repl } | 
    Set-Content $spec
nuget pack $spec -OutputDirectory "Nuget"
nuget push $file