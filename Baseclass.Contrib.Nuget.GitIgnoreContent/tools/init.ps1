param($installPath, $toolsPath, $package)

$modulePath = Join-Path $toolsPath Baseclass.Contrib.Nuget.GitIgnoreContent.psm1

Import-Module $modulePath

. (Join-Path $toolsPath 'GitIgnoreNugetContentRegisterEvents.ps1')