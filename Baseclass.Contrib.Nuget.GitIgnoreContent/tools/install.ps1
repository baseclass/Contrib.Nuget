param($installPath, $toolsPath, $package)

Function Create-RestoreTool {
	$modulePath = Join-Path $toolsPath Baseclass.Contrib.Nuget.GitIgnoreContent.psm1

	$module = Import-Module $modulePath -PassThru
	$packagesPath = Split-Path $installPath -Parent

	& $module { 
		param($packagesPath, $toolsPath)
		$restoreScriptPath = Join-Path $toolsPath 'RestoreNugetContent.ps1'
		$gitRepo = [System.Uri](Find-GitRepositoryPath $packagesPath)
		
		$relativeRestoreScriptPath = $gitRepo.MakeRelativeUri($restoreScriptPath).ToString().Replace('/', [System.IO.Path]::DirectorySeparatorChar)
		
		$restoreToolPath = Join-Path $gitRepo.LocalPath 'RestoreNugetContent.ps1'
		$powershellContent = ". '$relativeRestoreScriptPath'"
		
		Set-Content -Path $restoreToolPath $powershellContent
	} $packagesPath $toolsPath
}

Create-RestoreTool