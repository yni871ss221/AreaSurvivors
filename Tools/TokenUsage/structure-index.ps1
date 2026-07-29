param(
    [ValidateSet("Build", "EnsureFresh", "Query", "Status", "SelfTest")]
    [string]$Action = "Query",
    [ValidateSet("All", "CSharp", "PowerShell")]
    [string]$Language = "All",
    [string]$Symbol,
    [string]$Path,
    [ValidateRange(1, 50)]
    [int]$MaxResults = 20,
    [switch]$Force,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$IndexRoot = Join-Path $ProjectRoot "Library\AreaAgentIndex"
$IndexPath = Join-Path $IndexRoot "structure-index.json"
$ManifestPath = Join-Path $IndexRoot "csharp-manifest.txt"
$CSharpOutputPath = Join-Path $IndexRoot "csharp-changed.json"
$ToolProjectPath = Join-Path $PSScriptRoot "StructureIndexTool\StructureIndexTool.csproj"
$ToolSourcePath = Join-Path $PSScriptRoot "StructureIndexTool\Program.cs"
$ToolDllPath = Join-Path $IndexRoot "tool\bin\Release\net9.0\AreaStructureIndexTool.dll"
$ToolMarkerPath = Join-Path $IndexRoot "tool\tool-marker.json"
$script:HashAlgorithm = [System.Security.Cryptography.SHA256]::Create()

function Write-Utf8NoBom {
    param([string]$TargetPath, [string]$Text)

    $directory = Split-Path -Parent $TargetPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $TargetPath,
        $Text,
        [System.Text.UTF8Encoding]::new($false))
}

function Get-ContentHash {
    param([string]$TargetPath)

    $stream = [System.IO.File]::OpenRead($TargetPath)
    try {
        $bytes = $script:HashAlgorithm.ComputeHash($stream)
        return ([System.BitConverter]::ToString($bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
    }
}

function Get-RelativeProjectPath {
    param([string]$TargetPath)

    $fullPath = [System.IO.Path]::GetFullPath($TargetPath)
    if (-not $fullPath.StartsWith($ProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the project root: $TargetPath"
    }
    return $fullPath.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("\", "/")
}

function Get-SourceFileRecords {
    param([string]$SelectedLanguage)

    $records = @()
    if ($SelectedLanguage -in @("All", "CSharp")) {
        $csharpRoot = Join-Path $ProjectRoot "Assets\AreaSurvivors"
        $records += @(
            Get-ChildItem -LiteralPath $csharpRoot -Recurse -File -Filter "*.cs" |
                ForEach-Object {
                    [pscustomobject]@{
                        path = Get-RelativeProjectPath $_.FullName
                        fullPath = $_.FullName
                        language = "CSharp"
                        length = $_.Length
                        lastWriteUtc = $_.LastWriteTimeUtc.ToString("o")
                        sha256 = Get-ContentHash $_.FullName
                    }
                }
        )
    }
    if ($SelectedLanguage -in @("All", "PowerShell")) {
        $powershellRoot = Join-Path $ProjectRoot "Tools"
        $records += @(
            Get-ChildItem -LiteralPath $powershellRoot -Recurse -File -Filter "*.ps1" |
                ForEach-Object {
                    [pscustomobject]@{
                        path = Get-RelativeProjectPath $_.FullName
                        fullPath = $_.FullName
                        language = "PowerShell"
                        length = $_.Length
                        lastWriteUtc = $_.LastWriteTimeUtc.ToString("o")
                        sha256 = Get-ContentHash $_.FullName
                    }
                }
        )
    }
    return @($records | Sort-Object path)
}

function Get-ToolSourceHash {
    $combined = (Get-ContentHash $ToolProjectPath) + (Get-ContentHash $ToolSourcePath)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($combined)
    $hash = $script:HashAlgorithm.ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
}

function Ensure-CSharpTool {
    $sourceHash = Get-ToolSourceHash
    $marker = $null
    if (Test-Path -LiteralPath $ToolMarkerPath) {
        try {
            $marker = Get-Content -LiteralPath $ToolMarkerPath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        catch {
            $marker = $null
        }
    }

    if ((Test-Path -LiteralPath $ToolDllPath) -and
        $null -ne $marker -and
        $marker.sourceHash -eq $sourceHash) {
        return
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        throw "dotnet SDK is required to build the Roslyn structure index helper."
    }

    [System.IO.Directory]::CreateDirectory($IndexRoot) | Out-Null
    $baseOutputPath = (Join-Path $IndexRoot "tool\bin").Replace("\", "/") + "/"
    $baseIntermediatePath = (Join-Path $IndexRoot "tool\obj").Replace("\", "/") + "/"
    $buildOutput = @(
        & $dotnet.Source build $ToolProjectPath `
            -c Release `
            --nologo `
            --verbosity quiet `
            "-p:BaseOutputPath=$baseOutputPath" `
            "-p:BaseIntermediateOutputPath=$baseIntermediatePath" `
            "-p:MSBuildProjectExtensionsPath=$baseIntermediatePath" 2>&1
    )
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $ToolDllPath)) {
        $tail = @($buildOutput | Select-Object -Last 20) -join [Environment]::NewLine
        throw "C# structure index helper build failed.`n$tail"
    }

    Write-Utf8NoBom $ToolMarkerPath (
        [pscustomobject]@{
            sourceHash = $sourceHash
            builtAtUtc = [DateTime]::UtcNow.ToString("o")
        } | ConvertTo-Json -Compress
    )
}

function Get-ParameterMandatory {
    param([System.Management.Automation.Language.ParameterAst]$Parameter)

    foreach ($attribute in $Parameter.Attributes) {
        if ($attribute.TypeName.Name -ne "Parameter") {
            continue
        }
        $mandatory = @($attribute.NamedArguments | Where-Object ArgumentName -eq "Mandatory")
        if ($mandatory.Count -eq 0) {
            continue
        }
        if ($null -eq $mandatory[0].Argument) {
            return $true
        }
        return $mandatory[0].Argument.Extent.Text -notmatch '^\s*\$?false\s*$'
    }
    return $false
}

function Get-PowerShellMutationKinds {
    param(
        [object[]]$Commands,
        [string]$SourceText
    )

    $names = @($Commands | ForEach-Object name)
    $kinds = @()
    if (@($names | Where-Object { $_ -in @(
        "Set-Content", "Add-Content", "Out-File", "Export-Csv", "Export-Clixml",
        "Copy-Item", "Move-Item", "Rename-Item", "New-Item"
    ) }).Count -gt 0 -or $SourceText -match '\b(WriteAllText|WriteAllLines|WriteBytes)\s*\(') {
        $kinds += "filesystem-write"
    }
    if (@($names | Where-Object { $_ -in @("Remove-Item", "Clear-Content") }).Count -gt 0 -or
        $SourceText -match '\b(Delete|DeleteDirectory)\s*\(') {
        $kinds += "filesystem-delete"
    }
    if ($SourceText -match '(?im)(^|\s)git(\.exe)?\s+(add|commit|push|checkout|switch|merge|rebase|reset|clean)\b') {
        $kinds += "git"
    }
    if ($SourceText -match '(?i)\b(unicli|safe-unity|invoke-areasafeunity|invoke-unity-editor-runner)\b') {
        $kinds += "unity"
    }
    if (@($names | Where-Object { $_ -in @("Start-Process", "Stop-Process") }).Count -gt 0) {
        $kinds += "process"
    }
    return @($kinds | Sort-Object -Unique)
}

function Get-PowerShellFunctionEntries {
    param([System.Management.Automation.Language.Ast]$Ast)

    return @(
        $Ast.FindAll(
            { param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] },
            $true) |
            ForEach-Object {
                $functionAst = $_
                $functionParameters = @()
                if ($null -ne $functionAst.Parameters) {
                    $functionParameters += @($functionAst.Parameters)
                }
                if ($null -ne $functionAst.Body.ParamBlock) {
                    $functionParameters += @($functionAst.Body.ParamBlock.Parameters)
                }
                [pscustomobject]@{
                    name = $functionAst.Name
                    line = $functionAst.Extent.StartLineNumber
                    parameters = @(
                        $functionParameters |
                            Where-Object { $_ -is [System.Management.Automation.Language.ParameterAst] } |
                            ForEach-Object { $_.Name.VariablePath.UserPath } |
                            Sort-Object -Unique
                    )
                }
            }
    )
}

function Convert-PowerShellFile {
    param([object]$Record)

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $Record.fullPath,
        [ref]$tokens,
        [ref]$parseErrors)
    $sourceText = [System.IO.File]::ReadAllText($Record.fullPath)

    $parameters = @()
    if ($null -ne $ast.ParamBlock) {
        $parameters = @(
            $ast.ParamBlock.Parameters | ForEach-Object {
                [pscustomobject]@{
                    name = $_.Name.VariablePath.UserPath
                    type = $_.StaticType.FullName
                    mandatory = Get-ParameterMandatory $_
                    default = if ($null -ne $_.DefaultValue) { $_.DefaultValue.Extent.Text } else { $null }
                    line = $_.Extent.StartLineNumber
                }
            }
        )
    }

    $functions = @(Get-PowerShellFunctionEntries $ast)

    $commands = @(
        $ast.FindAll(
            { param($node) $node -is [System.Management.Automation.Language.CommandAst] },
            $true) |
            ForEach-Object {
                $commandName = $_.GetCommandName()
                if (-not [string]::IsNullOrWhiteSpace($commandName)) {
                    [pscustomobject]@{
                        name = $commandName
                        line = $_.Extent.StartLineNumber
                    }
                }
            } |
            Group-Object name |
            ForEach-Object {
                [pscustomobject]@{
                    name = $_.Name
                    firstLine = ($_.Group | Measure-Object line -Minimum).Minimum
                    count = $_.Count
                }
            } |
            Sort-Object name
    )

    $variables = @(
        $ast.FindAll(
            { param($node) $node -is [System.Management.Automation.Language.VariableExpressionAst] },
            $true) |
            ForEach-Object {
                [pscustomobject]@{
                    name = $_.VariablePath.UserPath
                    line = $_.Extent.StartLineNumber
                }
            } |
            Group-Object name |
            ForEach-Object {
                [pscustomobject]@{
                    name = $_.Name
                    firstLine = ($_.Group | Measure-Object line -Minimum).Minimum
                    count = $_.Count
                }
            } |
            Sort-Object name
    )

    $calledScripts = @(
        [regex]::Matches($sourceText, '(?i)([A-Za-z0-9_.-]+\.ps1)') |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique
    )
    $outputs = @(
        $commands |
            Where-Object name -in @(
                "Write-Output", "Write-Host", "ConvertTo-Json", "Format-Table",
                "Format-List", "Out-File", "Set-Content"
            ) |
            ForEach-Object name |
            Sort-Object -Unique
    )

    return [pscustomobject]@{
        path = $Record.path
        language = "PowerShell"
        length = $Record.length
        lastWriteUtc = $Record.lastWriteUtc
        sha256 = $Record.sha256
        parameters = $parameters
        functions = $functions
        commands = $commands
        variables = $variables
        calledScripts = $calledScripts
        stateMutations = @(Get-PowerShellMutationKinds $commands $sourceText)
        outputs = $outputs
        parseErrors = @(
            $parseErrors |
                Select-Object -First 10 |
                ForEach-Object {
                    [pscustomobject]@{
                        line = $_.Extent.StartLineNumber
                        message = $_.Message
                    }
                }
        )
    }
}

function Read-ExistingIndex {
    if (-not (Test-Path -LiteralPath $IndexPath)) {
        return $null
    }
    try {
        return Get-Content -LiteralPath $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Expand-Sequence {
    param([object]$Value)

    if ($null -eq $Value) {
        return
    }
    if ($Value -is [array]) {
        foreach ($item in $Value) {
            Write-Output $item
        }
        return
    }
    Write-Output $Value
}

function Add-CSharpEntryHash {
    param(
        [object]$Entry,
        [hashtable]$RecordByPath
    )

    $entryPath = [string]$Entry.path
    if (-not $RecordByPath.ContainsKey($entryPath)) {
        $preview = if ($entryPath.Length -le 160) { $entryPath } else { $entryPath.Substring(0, 160) + "..." }
        throw "C# helper returned an unexpected path (length=$($entryPath.Length)): $preview"
    }
    $matchingRecord = $RecordByPath[$entryPath]
    $entryHash = [string]$matchingRecord.sha256
    if ($entryHash -notmatch '^[0-9a-f]{64}$') {
        throw "C# index hash must be one SHA-256 value: $entryPath"
    }
    $Entry | Add-Member -NotePropertyName sha256 -NotePropertyValue $entryHash
    return $Entry
}

function Get-StaleState {
    param([string]$SelectedLanguage)

    $records = @(Get-SourceFileRecords $SelectedLanguage)
    $index = Read-ExistingIndex
    $existing = @{}
    if ($null -ne $index) {
        foreach ($entry in @($index.files)) {
            $existing[$entry.path] = $entry
        }
    }
    $csharpIndexerHash = Get-ToolSourceHash
    $powerShellIndexerHash = Get-ContentHash $PSCommandPath
    $existingCSharpIndexerHash = if ($null -ne $index -and
        $null -ne $index.PSObject.Properties["csharpIndexerHash"]) {
        [string]$index.csharpIndexerHash
    }
    else {
        ""
    }
    $existingPowerShellIndexerHash = if ($null -ne $index -and
        $null -ne $index.PSObject.Properties["powerShellIndexerHash"]) {
        [string]$index.powerShellIndexerHash
    }
    else {
        ""
    }

    $changed = @()
    $unchanged = @()
    foreach ($record in $records) {
        $indexerChanged =
            ($record.language -eq "CSharp" -and $existingCSharpIndexerHash -ne $csharpIndexerHash) -or
            ($record.language -eq "PowerShell" -and $existingPowerShellIndexerHash -ne $powerShellIndexerHash)
        if ($indexerChanged -or
            -not $existing.ContainsKey($record.path) -or
            $existing[$record.path].sha256 -ne $record.sha256) {
            $changed += $record
        }
        else {
            $unchanged += $record
        }
    }
    $currentPaths = @{}
    foreach ($record in $records) {
        $currentPaths[$record.path] = $true
    }
    $deleted = @(
        $existing.Values |
            Where-Object {
                $_.language -in @(
                    if ($SelectedLanguage -eq "All") { "CSharp", "PowerShell" } else { $SelectedLanguage }
                ) -and -not $currentPaths.ContainsKey($_.path)
            } |
            ForEach-Object path
    )

    return [pscustomobject]@{
        records = $records
        existingIndex = $index
        existingByPath = $existing
        changed = $changed
        unchanged = $unchanged
        deleted = $deleted
        csharpIndexerHash = $csharpIndexerHash
        powerShellIndexerHash = $powerShellIndexerHash
    }
}

function Update-StructureIndex {
    param(
        [string]$SelectedLanguage,
        [switch]$ForceRefresh
    )

    [System.IO.Directory]::CreateDirectory($IndexRoot) | Out-Null
    $state = Get-StaleState $SelectedLanguage
    if ($ForceRefresh) {
        $state.changed = @($state.records)
        $state.unchanged = @()
    }

    $newEntries = @()
    foreach ($record in @($state.unchanged)) {
        $newEntries += $state.existingByPath[$record.path]
    }

    $changedCSharp = @($state.changed | Where-Object language -eq "CSharp")
    if ($changedCSharp.Count -gt 0) {
        Ensure-CSharpTool
        Write-Utf8NoBom $ManifestPath (($changedCSharp | ForEach-Object fullPath) -join [Environment]::NewLine)
        $dotnet = Get-Command dotnet -ErrorAction Stop
        $toolOutput = @(
            & $dotnet.Source $ToolDllPath index `
                --project-root $ProjectRoot `
                --manifest $ManifestPath `
                --output $CSharpOutputPath 2>&1
        )
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $CSharpOutputPath)) {
            $tail = @($toolOutput | Select-Object -Last 20) -join [Environment]::NewLine
            throw "C# structure indexing failed.`n$tail"
        }
        $parsedCSharpEntries = Get-Content -LiteralPath $CSharpOutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $csharpEntries = @(Expand-Sequence $parsedCSharpEntries)
        $recordByPath = @{}
        foreach ($record in $changedCSharp) {
            $recordByPath[$record.path] = $record
        }
        foreach ($entry in $csharpEntries) {
            $newEntries += Add-CSharpEntryHash $entry $recordByPath
        }
    }

    foreach ($record in @($state.changed | Where-Object language -eq "PowerShell")) {
        $newEntries += Convert-PowerShellFile $record
    }

    if ($SelectedLanguage -ne "All" -and $null -ne $state.existingIndex) {
        $otherLanguage = if ($SelectedLanguage -eq "CSharp") { "PowerShell" } else { "CSharp" }
        $newEntries += @($state.existingIndex.files | Where-Object language -eq $otherLanguage)
    }

    $files = @($newEntries | Sort-Object path)
    $index = [pscustomobject]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString("o")
        csharpIndexerHash = $state.csharpIndexerHash
        powerShellIndexerHash = $state.powerShellIndexerHash
        sourceRoots = @(
            "Assets/AreaSurvivors/**/*.cs"
            "Tools/**/*.ps1"
        )
        files = $files
    }
    Write-Utf8NoBom $IndexPath ($index | ConvertTo-Json -Depth 20 -Compress)

    return [pscustomobject]@{
        index = $index
        totalFiles = $files.Count
        updatedFiles = @($state.changed).Count
        unchangedFiles = @($state.unchanged).Count
        deletedFiles = @($state.deleted).Count
        indexPath = Get-RelativeProjectPath $IndexPath
    }
}

function New-QueryResult {
    param(
        [object]$Index,
        [string]$SelectedLanguage,
        [string]$QuerySymbol,
        [string]$QueryPath,
        [int]$Limit
    )

    $files = @($Index.files)
    if ($SelectedLanguage -ne "All") {
        $files = @($files | Where-Object language -eq $SelectedLanguage)
    }

    if (-not [string]::IsNullOrWhiteSpace($QueryPath)) {
        $normalizedPath = $QueryPath.Replace("\", "/")
        $matches = @($files | Where-Object { $_.path -like "*$normalizedPath*" })
        $details = @()
        foreach ($file in @($matches | Select-Object -First $Limit)) {
            if ($file.language -eq "CSharp") {
                $members = @()
                foreach ($type in @($file.types)) {
                    foreach ($member in @($type.members)) {
                        $members += [pscustomobject]@{
                            line = $member.line
                            type = $type.name
                            kind = $member.kind
                            name = $member.name
                            signature = $member.signature
                            serialized = $member.serialized
                        }
                    }
                }
                $details += [pscustomobject]@{
                    path = $file.path
                    language = $file.language
                    namespaces = @($file.namespaces)
                    types = @($file.types | Select-Object -First $Limit | Select-Object name, fullName, kind, accessibility, bases, unityKind, line)
                    typeCount = @($file.types).Count
                    members = @($members | Sort-Object line | Select-Object -First $Limit)
                    memberCount = $members.Count
                    menuItems = @($file.menuItems | Select-Object -First $Limit)
                    menuItemCount = @($file.menuItems).Count
                    parseErrors = @($file.parseErrors)
                }
            }
            else {
                $details += [pscustomobject]@{
                    path = $file.path
                    language = $file.language
                    parameters = @($file.parameters | Select-Object -First $Limit)
                    parameterCount = @($file.parameters).Count
                    functions = @($file.functions | Select-Object -First $Limit)
                    functionCount = @($file.functions).Count
                    commands = @($file.commands | Select-Object -First $Limit)
                    commandCount = @($file.commands).Count
                    calledScripts = @($file.calledScripts | Select-Object -First $Limit)
                    calledScriptCount = @($file.calledScripts).Count
                    stateMutations = @($file.stateMutations)
                    outputs = @($file.outputs)
                    parseErrors = @($file.parseErrors)
                }
            }
        }
        return [pscustomobject]@{
            mode = "path"
            query = $QueryPath
            matchCount = $matches.Count
            results = $details
        }
    }

    $definitions = @()
    $references = @()
    foreach ($file in $files) {
        if ($file.language -eq "CSharp") {
            foreach ($type in @($file.types)) {
                if ($type.name -ceq $QuerySymbol -or $type.fullName -ceq $QuerySymbol) {
                    $definitions += [pscustomobject]@{
                        path = $file.path
                        line = $type.line
                        kind = $type.kind
                        signature = "$($type.accessibility) $($type.kind) $($type.fullName)"
                        unityKind = $type.unityKind
                    }
                }
                foreach ($member in @($type.members)) {
                    if ($member.name -ceq $QuerySymbol) {
                        $definitions += [pscustomobject]@{
                            path = $file.path
                            line = $member.line
                            kind = $member.kind
                            signature = "$($type.name).$($member.signature)"
                            unityKind = $null
                        }
                    }
                }
            }
            foreach ($reference in @($file.references)) {
                if ($reference.name -ceq $QuerySymbol) {
                    $references += [pscustomobject]@{
                        path = $file.path
                        line = $reference.firstLine
                        kind = "identifier"
                        count = $reference.count
                    }
                }
            }
        }
        else {
            foreach ($parameter in @($file.parameters)) {
                if ($parameter.name -ieq $QuerySymbol) {
                    $definitions += [pscustomobject]@{
                        path = $file.path
                        line = $parameter.line
                        kind = "parameter"
                        signature = "$($parameter.type) `$$($parameter.name)"
                        unityKind = $null
                    }
                }
            }
            foreach ($function in @($file.functions)) {
                if ($function.name -ieq $QuerySymbol) {
                    $definitions += [pscustomobject]@{
                        path = $file.path
                        line = $function.line
                        kind = "function"
                        signature = "$($function.name)($($function.parameters -join ', '))"
                        unityKind = $null
                    }
                }
            }
            foreach ($command in @($file.commands)) {
                if ($command.name -ieq $QuerySymbol) {
                    $references += [pscustomobject]@{
                        path = $file.path
                        line = $command.firstLine
                        kind = "command"
                        count = $command.count
                    }
                }
            }
            foreach ($variable in @($file.variables)) {
                if ($variable.name -ieq $QuerySymbol) {
                    $references += [pscustomobject]@{
                        path = $file.path
                        line = $variable.firstLine
                        kind = "variable"
                        count = $variable.count
                    }
                }
            }
        }
    }

    return [pscustomobject]@{
        mode = "symbol"
        query = $QuerySymbol
        definitionCount = $definitions.Count
        referenceFileCount = $references.Count
        definitions = @($definitions | Sort-Object path, line | Select-Object -First $Limit)
        references = @($references | Sort-Object path, line | Select-Object -First $Limit)
    }
}

function Write-QueryText {
    param([object]$Result)

    Write-Output ("structure_index_query: {0}={1}" -f $Result.mode, $Result.query)
    if ($Result.mode -eq "symbol") {
        Write-Output ("definitions: {0}; reference_files: {1}" -f $Result.definitionCount, $Result.referenceFileCount)
        foreach ($definition in @($Result.definitions)) {
            $unity = if ([string]::IsNullOrWhiteSpace($definition.unityKind)) { "" } else { " [$($definition.unityKind)]" }
            Write-Output ("D {0}:{1} {2}{3}" -f $definition.path, $definition.line, $definition.signature, $unity)
        }
        foreach ($reference in @($Result.references)) {
            Write-Output ("R {0}:{1} {2} x{3}" -f $reference.path, $reference.line, $reference.kind, $reference.count)
        }
        return
    }

    Write-Output ("matched_files: {0}" -f $Result.matchCount)
    foreach ($file in @($Result.results)) {
        Write-Output ("F {0} [{1}]" -f $file.path, $file.language)
        if ($file.language -eq "CSharp") {
            Write-Output ("A types={0}; members={1}; shown={2}; menu_items={3}; parse_errors={4}" -f
                $file.typeCount,
                $file.memberCount,
                @($file.members).Count,
                $file.menuItemCount,
                @($file.parseErrors).Count)
            foreach ($type in @($file.types)) {
                $bases = if (@($type.bases).Count -gt 0) { " : " + ($type.bases -join ", ") } else { "" }
                $unity = if ([string]::IsNullOrWhiteSpace($type.unityKind)) { "" } else { " [$($type.unityKind)]" }
                Write-Output ("T {0}:{1} {2} {3}{4}{5}" -f $file.path, $type.line, $type.kind, $type.fullName, $bases, $unity)
            }
            foreach ($member in @($file.members)) {
                $serialized = if ($member.serialized) { " [serialized]" } else { "" }
                Write-Output ("M {0}:{1} {2}.{3} {4}{5}" -f $file.path, $member.line, $member.type, $member.name, $member.signature, $serialized)
            }
            foreach ($menuItem in @($file.menuItems)) {
                Write-Output ("U {0}:{1} MenuItem {2}" -f $file.path, $menuItem.line, $menuItem.path)
            }
        }
        else {
            Write-Output ("A parameters={0}; functions={1}; commands={2}; called_scripts={3}; parse_errors={4}" -f
                $file.parameterCount,
                $file.functionCount,
                $file.commandCount,
                $file.calledScriptCount,
                @($file.parseErrors).Count)
            foreach ($parameter in @($file.parameters)) {
                Write-Output ("P {0}:{1} {2} -{3} mandatory={4} default={5}" -f $file.path, $parameter.line, $parameter.type, $parameter.name, $parameter.mandatory, $parameter.default)
            }
            foreach ($function in @($file.functions)) {
                Write-Output ("N {0}:{1} function {2}({3})" -f $file.path, $function.line, $function.name, ($function.parameters -join ", "))
            }
            if (@($file.calledScripts).Count -gt 0) {
                Write-Output ("C scripts: {0}" -f ($file.calledScripts -join ", "))
            }
            if (@($file.stateMutations).Count -gt 0) {
                Write-Output ("S mutations: {0}" -f ($file.stateMutations -join ", "))
            }
            if (@($file.outputs).Count -gt 0) {
                Write-Output ("O outputs: {0}" -f ($file.outputs -join ", "))
            }
        }
        foreach ($parseError in @($file.parseErrors)) {
            Write-Output ("E {0}:{1} {2}" -f $file.path, $parseError.line, $parseError.message)
        }
    }
}

function Invoke-SelfTest {
    Ensure-CSharpTool
    $dotnet = Get-Command dotnet -ErrorAction Stop
    $csharpOutput = @(& $dotnet.Source $ToolDllPath self-test 2>&1)
    if ($LASTEXITCODE -ne 0 -or $csharpOutput -notcontains "structure_index_csharp_self_test: pass") {
        throw "C# structure index self-test failed: $($csharpOutput -join [Environment]::NewLine)"
    }

    $tokens = $null
    $errors = $null
    $fixture = @'
param([Parameter(Mandatory)][string]$Path)
function Invoke-Sample { param([int]$Count) Write-Output $Count }
Set-Content -LiteralPath $Path -Value "ok"
'@
    $ast = [System.Management.Automation.Language.Parser]::ParseInput($fixture, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) {
        throw "PowerShell structure fixture did not parse."
    }
    $functions = @(Get-PowerShellFunctionEntries $ast)
    $commands = @(
        $ast.FindAll(
            { param($node) $node -is [System.Management.Automation.Language.CommandAst] },
            $true) |
            ForEach-Object {
                [pscustomobject]@{ name = $_.GetCommandName(); line = $_.Extent.StartLineNumber }
            }
    )
    if ($functions.Count -ne 1 -or
        $functions[0].name -ne "Invoke-Sample" -or
        "Count" -notin @($functions[0].parameters) -or
        "filesystem-write" -notin @(Get-PowerShellMutationKinds $commands $fixture) -or
        "unity" -notin @(Get-PowerShellMutationKinds -Commands @() -SourceText "Invoke-AreaSafeUnity.ps1")) {
        throw "PowerShell AST structure extraction contract failed."
    }

    $hashFixture = "a" * 64
    $entryFixture = [pscustomobject]@{ path = "Fixture.cs" }
    $recordFixture = @{ "Fixture.cs" = [pscustomobject]@{ sha256 = $hashFixture } }
    $hashedEntry = Add-CSharpEntryHash $entryFixture $recordFixture
    if ($hashedEntry.sha256 -is [array] -or $hashedEntry.sha256 -ne $hashFixture) {
        throw "C# per-file hash attachment contract failed."
    }

    $jsonArrayFixture = '[{"path":"A.cs"},{"path":"B.cs"}]' | ConvertFrom-Json
    $expandedFixture = @(Expand-Sequence $jsonArrayFixture)
    if ($expandedFixture.Count -ne 2 -or $expandedFixture[1].path -ne "B.cs") {
        throw "Windows PowerShell JSON array expansion contract failed."
    }

    $textResultFixture = [pscustomobject]@{
        mode = "path"
        query = "Fixture.ps1"
        matchCount = 1
        results = @(
            [pscustomobject]@{
                path = "Fixture.ps1"
                language = "PowerShell"
                parameterCount = 1
                functionCount = 0
                commandCount = 0
                calledScriptCount = 0
                parameters = @(
                    [pscustomobject]@{
                        line = 1
                        type = "System.String"
                        name = "Path"
                        mandatory = $true
                        default = $null
                    }
                )
                functions = @()
                calledScripts = @()
                stateMutations = @()
                outputs = @()
                parseErrors = @()
            }
        )
    }
    $textOutputFixture = @(Write-QueryText $textResultFixture)
    if ($textOutputFixture -notcontains "P Fixture.ps1:1 System.String -Path mandatory=True default=") {
        throw "Compact query text formatting contract failed."
    }

    $csharpTextResultFixture = [pscustomobject]@{
        mode = "path"
        query = "Fixture.cs"
        matchCount = 1
        results = @(
            [pscustomobject]@{
                path = "Fixture.cs"
                language = "CSharp"
                typeCount = 0
                memberCount = 1
                members = @(
                    [pscustomobject]@{
                        line = 3
                        type = "Fixture"
                        kind = "field"
                        name = "count"
                        signature = "private int count"
                        serialized = $true
                    }
                )
                types = @()
                menuItems = @()
                menuItemCount = 0
                parseErrors = @()
            }
        )
    }
    $csharpTextOutputFixture = @(Write-QueryText $csharpTextResultFixture)
    if ($csharpTextOutputFixture -notcontains "A types=0; members=1; shown=1; menu_items=0; parse_errors=0" -or
        $csharpTextOutputFixture -notcontains "M Fixture.cs:3 Fixture.count private int count [serialized]") {
        throw "Compact C# query text formatting contract failed."
    }
    Write-Output "structure_index_self_test: pass"
}

try {
    switch ($Action) {
        "SelfTest" {
            Invoke-SelfTest
            break
        }
        "Status" {
            $state = Get-StaleState $Language
            $status = [pscustomobject]@{
                indexExists = Test-Path -LiteralPath $IndexPath
                sourceFiles = @($state.records).Count
                staleFiles = @($state.changed).Count
                deletedFiles = @($state.deleted).Count
                parseErrorFiles = if ($null -eq $state.existingIndex) {
                    0
                }
                else {
                    @($state.existingIndex.files | Where-Object { @($_.parseErrors).Count -gt 0 }).Count
                }
                indexPath = Get-RelativeProjectPath $IndexPath
            }
            if ($Json) {
                $status | ConvertTo-Json -Depth 5
            }
            else {
                Write-Output ("structure_index_status: exists={0}; sources={1}; stale={2}; deleted={3}; parse_error_files={4}; path={5}" -f
                    $status.indexExists,
                    $status.sourceFiles,
                    $status.staleFiles,
                    $status.deletedFiles,
                    $status.parseErrorFiles,
                    $status.indexPath)
            }
            break
        }
        { $_ -in @("Build", "EnsureFresh") } {
            $update = Update-StructureIndex $Language -ForceRefresh:$Force
            if ($Json) {
                $update | ConvertTo-Json -Depth 5
            }
            else {
                Write-Output ("structure_index_build: total={0}; updated={1}; unchanged={2}; deleted={3}; path={4}" -f
                    $update.totalFiles,
                    $update.updatedFiles,
                    $update.unchangedFiles,
                    $update.deletedFiles,
                    $update.indexPath)
            }
            break
        }
        "Query" {
            if ([string]::IsNullOrWhiteSpace($Symbol) -eq [string]::IsNullOrWhiteSpace($Path)) {
                throw "Query requires exactly one of -Symbol or -Path."
            }
            $update = Update-StructureIndex $Language
            $result = New-QueryResult $update.index $Language $Symbol $Path $MaxResults
            if ($Json) {
                [pscustomobject]@{
                    freshness = [pscustomobject]@{
                        updatedFiles = $update.updatedFiles
                        unchangedFiles = $update.unchangedFiles
                        deletedFiles = $update.deletedFiles
                    }
                    query = $result
                } | ConvertTo-Json -Depth 20
            }
            else {
                Write-Output ("structure_index_freshness: updated={0}; unchanged={1}; deleted={2}" -f
                    $update.updatedFiles,
                    $update.unchangedFiles,
                    $update.deletedFiles)
                Write-QueryText $result
            }
            break
        }
    }
}
finally {
    $script:HashAlgorithm.Dispose()
}
