Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

function Add-LinkedAsExisting {
	[CmdletBinding()]
	[OutputType([System.String])]
	param(
		[Parameter(Position=0, Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[System.String]
		$installPath,

		[Parameter(Position=1, Mandatory=$true)]
		[ValidateNotNull()]
		[System.__ComObject]
		$project,
		
		[Parameter(Position=2, Mandatory=$true)]
		[ValidateNotNull()]
		$package
	)
	
	LinkedAsExisting $installPath $project $package $true
}
Export-ModuleMember -Function Add-LinkedAsExisting

New-Alias -Name alae -Value Add-LinkedAsExisting
if ($?) {
	Export-ModuleMember -Alias alae
}

function Remove-LinkedAsExisting {
	[CmdletBinding()]
	[OutputType([System.String])]
	param(
		[Parameter(Position=0, Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[System.String]
		$installPath,

		[Parameter(Position=1, Mandatory=$true)]
		[ValidateNotNull()]
		[System.__ComObject]
		$project,
		
		[Parameter(Position=2, Mandatory=$true)]
		[ValidateNotNull()]
		$package
	)
	
	LinkedAsExisting $installPath $project $package $false
}
Export-ModuleMember -Function Remove-LinkedAsExisting

New-Alias -Name rlae -Value Remove-LinkedAsExisting
if ($?) {
	Export-ModuleMember -Alias rlae
}

function LinkedAsExisting {
	[CmdletBinding()]
	[OutputType([System.String])]
	param(
		[Parameter(Position=0, Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[System.String]
		$installPath,

		[Parameter(Position=1, Mandatory=$true)]
		[ValidateNotNull()]
		[System.__ComObject]
		$project,
		
		[Parameter(Position=2, Mandatory=$true)]
		[ValidateNotNull()]
		$package,
		
		[Parameter(Position=3, Mandatory=$true)]
		[ValidateNotNull()]
		[bool]
		$add
	)
	
	if(![System.IO.Path]::IsPathRooted($installPath))
	{
		$installPath = Join-Path $pwd $installPath
	}
	
	
	$linkedPath = (Join-Path $installPath "Linked\")
	
	Write-Host "Adding linked folder as existing..."
	Write-Host $linkedPath
	
	$projectUri = New-Object -TypeName Uri -ArgumentList "file://$($project.FullName)"	

	$msbProject = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName) | Select-Object -First 1
	
	$files = $package.GetFiles()

	$regex = New-Object -TypeName Regex -ArgumentList "^Linked\\"

	foreach($file in $($files | Where-Object { $_.Path.StartsWith("Linked\") }))
	{
	    $relativePath = GetRelativePath $projectUri $installPath $file
		$projectRelativePath = $regex.Replace($file.Path, '')
		if($add)
		{
			Write-Host "Adding $relativePath as $projectRelativePath"
			AddLinkedItem $msbProject $relativePath $projectRelativePath
		} else {
			Write-Host "Removing $relativePath"
			RemoveLinkedItem $msbProject $relativePath
		}
	}
	
	$msbProject.Save()
	$project.Save()
}

function RemoveLinkedItem()
{
	param(
		[Parameter(Position=0, Mandatory=$true)]
		$msbProject, 
		[Parameter(Position=1, Mandatory=$true)]
		$relativePath
	)
	
	$item = $msbProject.Items | Where-Object { $_.EvaluatedInclude -eq $relativePath } | Select-Object -First 1
	
	if($item -eq $null) #VisualStudio 2013
	{
		Write-Host "Selecting items to remove for VisualStudio 2013"
		$item = $msbProject.Items | %{$_.Value} | Where-Object { $_.EvaluatedInclude -eq $relativePath } | Select-Object -First 1
	}
	
	if($item -ne $null)
	{
		Write-Host "Remove $relativePath from project"
		$msbProject.RemoveItem($item)
	} else {
		Write-Host "Items $relativePath not found !"
	}
}

function AddLinkedItem()
{
	param(
		[Parameter(Position=0, Mandatory=$true)]
		$msbProject, 
		[Parameter(Position=1, Mandatory=$true)]
		$relativePath, 
		[Parameter(Position=2, Mandatory=$true)]
		$projectRelativePath)
	
	$metadata=New-Object "System.Collections.Generic.Dictionary``2[System.String,System.String]"
	$metadata.Add("Link", $projectRelativePath)	
	
	if($projectRelativePath.EndsWith(".tt"))
	{	
		$metadata.Add("Generator", "TextTemplatingFileGenerator")		
	}
	
	$msbProject.AddItem("Content", $relativePath, $metadata)
}

function GetRelativePath()
{
	param(
		[Parameter(Position=0, Mandatory=$true)]
		$projectUri, 
		[Parameter(Position=1, Mandatory=$true)]
		$packagePath,
		[Parameter(Position=2, Mandatory=$true)]
		$file)
	$fileUri = New-Object -TypeName Uri -ArgumentList "file://$(Join-Path $packagePath $file.Path)"
	return $projectUri.MakeRelativeUri($fileUri) -replace '/','\'
}
