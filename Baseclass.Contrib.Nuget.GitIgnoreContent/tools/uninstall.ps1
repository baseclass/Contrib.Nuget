param($installPath, $toolsPath, $package)

Function Delete-RestoreTool {
	$modulePath = Join-Path $toolsPath Baseclass.Contrib.Nuget.GitIgnoreContent.psm1

	$module = Import-Module $modulePath -PassThru
	$packagesPath = Split-Path $installPath -Parent

	& $module { 
		param($packagesPath)
		$gitRepo = [System.Uri](Find-GitRepositoryPath $packagesPath)
		
		$restoreToolPath = Join-Path $gitRepo.LocalPath 'RestoreNugetContent.ps1'
		
		Remove-Item $restoreToolPath
	} $packagesPath
}

Delete-RestoreTool