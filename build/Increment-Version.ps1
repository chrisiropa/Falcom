param(
   [Parameter(Mandatory = $true)]
   [string]$VersionFile
)

$ErrorActionPreference = 'Stop'

$currentVersionText = [System.IO.File]::ReadAllText($VersionFile).Trim()
$parts = $currentVersionText.Split('.')

if ($parts.Length -ne 4)
{
   throw "Ungueltige Version '$currentVersionText' in '$VersionFile'. Erwartet wird Major.Minor.Patch.Build."
}

$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]
$currentBuild = [int]$parts[3]

if ($currentBuild -ge 65534)
{
   throw 'FalcomVersionBuild hat den maximalen Assembly-Versionswert erreicht.'
}

$nextBuild = $currentBuild + 1
$newVersion = "$major.$minor.$patch.$nextBuild"

[System.IO.File]::WriteAllText(
   $VersionFile,
   "$newVersion`r`n",
   [System.Text.UTF8Encoding]::new($false))

Write-Output $newVersion
