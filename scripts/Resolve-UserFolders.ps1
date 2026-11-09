# Run under the Windows account whose personal files you intend to inspect.
# A sandbox may inherit USERPROFILE and USERNAME while using a different HKCU.
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$environmentAccount = "$env:USERDOMAIN\$env:USERNAME"
$result = [ordered]@{
    ProcessIdentity = $identity.Name
    EnvironmentAccount = $environmentAccount
    UserProfile = $env:USERPROFILE
}

try {
    $environmentSid = ([Security.Principal.NTAccount]$environmentAccount).Translate(
        [Security.Principal.SecurityIdentifier])
}
catch {
    $result.Status = 'CannotVerifyEnvironmentAccount'
    $result.Message = 'Run this helper under the intended Windows account.'
    $result | ConvertTo-Json
    exit 2
}

if ($identity.User.Value -ne $environmentSid.Value) {
    $result.Status = 'IdentityMismatch'
    $result.Message = 'This process uses another account. Its HKCU and Documents folder are not the intended user folders. Rerun under the intended Windows account.'
    $result | ConvertTo-Json
    exit 2
}

$profile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$documents = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::MyDocuments,
    [Environment+SpecialFolderOption]::DoNotVerify)

if ([string]::IsNullOrWhiteSpace($profile) -or [string]::IsNullOrWhiteSpace($documents) -or
    [string]::IsNullOrWhiteSpace($env:USERPROFILE) -or
    -not [IO.Path]::GetFullPath($profile).TrimEnd('\', '/').Equals(
        [IO.Path]::GetFullPath($env:USERPROFILE).TrimEnd('\', '/'),
        [StringComparison]::OrdinalIgnoreCase)) {
    $result.Status = 'FolderMismatch'
    $result.Message = 'Windows did not return a profile and Documents folder consistent with USERPROFILE. Check this account before editing personal files.'
    $result | ConvertTo-Json
    exit 3
}

$result.Status = 'Verified'
$result.WindowsProfile = $profile
$result.Documents = $documents
$result.TabletFriendLayouts = Join-Path $documents 'TabletFriend\layouts'
$result | ConvertTo-Json
