param(
    [string[]]$ReferencedImagePath = @(),
    [Nullable[int]]$NumLastImagesToInclude,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function Assert-ImageGenRequest {
    param(
        [string[]]$Paths,
        [bool]$HasConversationImageCount,
        [Nullable[int]]$ConversationImageCount,
        [switch]$SkipPathValidation
    )

    $hasReferences = $null -ne $Paths -and $Paths.Count -gt 0
    if ($hasReferences -and $HasConversationImageCount) {
        throw "guard_code: 42; image_gen accepts only one image input mode. Omit num_last_images_to_include entirely when referenced_image_paths is used, including a zero value."
    }
    if ($HasConversationImageCount -and ($ConversationImageCount.Value -lt 1 -or $ConversationImageCount.Value -gt 5)) {
        throw "guard_code: 43; num_last_images_to_include must be between 1 and 5 when specified. Omit it for a new image or when referenced_image_paths is used."
    }
    if (-not $SkipPathValidation -and $hasReferences) {
        foreach ($path in $Paths) {
            if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "guard_code: 44; referenced image path must be an existing file: $path"
            }
        }
    }
}

if ($SelfTest) {
    $mutualExclusionGuarded = $false
    try {
        Assert-ImageGenRequest -Paths @("reference.png") -HasConversationImageCount $true -ConversationImageCount 0 -SkipPathValidation
    } catch {
        $mutualExclusionGuarded = $_.Exception.Message.Contains("guard_code: 42")
    }
    if (-not $mutualExclusionGuarded) { throw "image_gen mutual exclusion guard self-test failed." }

    $rangeGuarded = $false
    try {
        Assert-ImageGenRequest -Paths @() -HasConversationImageCount $true -ConversationImageCount 0 -SkipPathValidation
    } catch {
        $rangeGuarded = $_.Exception.Message.Contains("guard_code: 43")
    }
    if (-not $rangeGuarded) { throw "image_gen conversation image count guard self-test failed." }

    Assert-ImageGenRequest -Paths @("reference.png") -HasConversationImageCount $false -ConversationImageCount $null -SkipPathValidation
    Write-Output "imagegen_request_preflight_self_test: passed"
    exit 0
}

Assert-ImageGenRequest `
    -Paths $ReferencedImagePath `
    -HasConversationImageCount $PSBoundParameters.ContainsKey("NumLastImagesToInclude") `
    -ConversationImageCount $NumLastImagesToInclude

Write-Output "imagegen_request_preflight: passed"
