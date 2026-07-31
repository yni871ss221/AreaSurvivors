@{
    SchemaVersion = 1
    Operations = @{
        "Schema" = @{
            Risk = "ReadOnly"
            Implementation = ""
            Required = @()
            Allowed = @("Target")
        }
        "Code.Symbol" = @{
            Risk = "ReadOnly"
            Implementation = "structure-index.ps1"
            Required = @("Symbol")
            Allowed = @("Symbol", "Language", "MaxResults", "Force")
        }
        "Code.File" = @{
            Risk = "WritesCache"
            Implementation = "code-file-overview.ps1"
            Required = @("Path")
            Allowed = @("Path", "Language", "MaxResults", "Force")
            SinglePath = $true
        }
        "Code.Summary.Store" = @{
            Risk = "WritesCache"
            Implementation = "semantic-summary-cache.ps1"
            Required = @("Path", "ExpectedHash", "Purpose")
            Allowed = @(
                "Path",
                "ExpectedHash",
                "Purpose",
                "Flow",
                "Invariants",
                "SideEffects",
                "Verification"
            )
            SinglePath = $true
        }
        "Code.Summary.Stats" = @{
            Risk = "ReadOnly"
            Implementation = "semantic-summary-cache.ps1"
            Required = @()
            Allowed = @("MaxResults")
        }
        "Code.Read" = @{
            Risk = "ReadOnly"
            Implementation = "safe-read.ps1"
            Required = @("Path")
            Allowed = @(
                "Path",
                "StartLine",
                "EndLine",
                "Last",
                "Pattern",
                "Context",
                "MaxResults",
                "Ranges",
                "Literal",
                "PrintOutput"
            )
            SinglePath = $true
        }
        "Code.Search" = @{
            Risk = "ReadOnly"
            Implementation = "safe-search.ps1"
            Required = @("Pattern")
            Allowed = @(
                "Pattern",
                "Path",
                "SearchMode",
                "Context",
                "MaxResults",
                "Extension",
                "Literal",
                "PrintOutput"
            )
        }
        "Git.Diff" = @{
            Risk = "ReadOnly"
            Implementation = "safe-diff.ps1"
            Required = @()
            Allowed = @("Path", "RefRange", "DiffMode", "MaxResults", "PrintOutput")
        }
        "Git.Check" = @{
            Risk = "ReadOnly"
            Implementation = "scoped-diff-check.ps1"
            Required = @("Path")
            Allowed = @("Path", "Cached", "ExcludeUnityMeta", "PrintOutput")
        }
        "Git.Status" = @{
            Risk = "ReadOnly"
            Implementation = "safe-status.ps1"
            Required = @()
            Allowed = @("Path", "PrintOutput")
        }
        "Git.Log" = @{
            Risk = "ReadOnly"
            Implementation = "Invoke-AreaSafeCommand.ps1"
            Required = @()
            Allowed = @("MaxResults", "PrintOutput")
        }
        "Command.Guard" = @{
            Risk = "Diagnostic"
            Implementation = "Invoke-AreaGuardedCommand.ps1"
            Required = @("CommandText")
            Allowed = @(
                "CommandText",
                "DryRun",
                "ExecuteOriginalIfSafe",
                "PrintOutput"
            )
        }
        "Graph.Status" = @{
            Risk = "ReadOnly"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @()
            Allowed = @("PrintOutput")
        }
        "Graph.Ensure" = @{
            Risk = "WritesCache"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @()
            Allowed = @("PrintOutput")
        }
        "Graph.Update" = @{
            Risk = "WritesCache"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @()
            Allowed = @("PrintOutput")
        }
        "Graph.Explain" = @{
            Risk = "ReadOnly"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @("Source")
            Allowed = @("Source", "Budget", "PrintOutput")
        }
        "Graph.Path" = @{
            Risk = "ReadOnly"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @("Source", "Target")
            Allowed = @("Source", "Target", "Budget", "PrintOutput")
        }
        "Graph.Affected" = @{
            Risk = "ReadOnly"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @("Source")
            Allowed = @("Source", "Depth", "MaxResults", "PrintOutput")
        }
        "Graph.Query" = @{
            Risk = "ReadOnly"
            Implementation = "safe-graphify-pilot.ps1"
            Required = @("Question")
            Allowed = @(
                "Question",
                "GraphContext",
                "Budget",
                "PrintOutput"
            )
        }
        "Unity.Console" = @{
            Risk = "ReadOnlyUnity"
            Implementation = "safe-unity.ps1"
            Required = @()
            Allowed = @(
                "ConsoleLevel",
                "MaxResults",
                "TimeoutSeconds",
                "PrintOutput"
            )
        }
        "Unity.Compile" = @{
            Risk = "UnityMutation"
            Implementation = "safe-unity.ps1"
            Required = @()
            Allowed = @("TimeoutSeconds", "CompileWaitSeconds", "PrintOutput")
        }
        "Unity.Menu" = @{
            Risk = "UnityMutation"
            Implementation = "safe-unity.ps1"
            Required = @("MenuPath")
            Allowed = @("MenuPath", "TimeoutSeconds", "PrintOutput")
        }
        "Unity.Import" = @{
            Risk = "UnityMutation"
            Implementation = "safe-unity.ps1"
            Required = @("Path")
            Allowed = @("Path", "TimeoutSeconds", "PrintOutput")
            SinglePath = $true
        }
        "Unity.Refresh" = @{
            Risk = "UnityMutation"
            Implementation = "safe-unity.ps1"
            Required = @()
            Allowed = @("TimeoutSeconds", "PrintOutput")
        }
        "Unity.Search" = @{
            Risk = "ReadOnlyUnity"
            Implementation = "safe-unity-search.ps1"
            Required = @("Pattern")
            Allowed = @("Pattern", "PrintOutput")
        }
        "Unity.Report" = @{
            Risk = "UnityMutation"
            Implementation = "run-unity-report.ps1"
            Required = @("ReportName")
            Allowed = @("ReportName")
        }
        "Unity.Validate" = @{
            Risk = "UnityMutation"
            Implementation = "invoke-menu-validator.ps1"
            Required = @("MenuPath")
            Allowed = @(
                "MenuPath",
                "ResultWaitSeconds",
                "PrintOutput"
            )
        }
        "Unity.Runner" = @{
            Risk = "UnityMutation"
            Implementation = "invoke-unity-editor-runner.ps1"
            Required = @("Phase", "ScriptPath")
            Allowed = @(
                "Phase",
                "ScriptPath",
                "DependencyPaths",
                "MenuPath",
                "ImportTimeoutSeconds",
                "TimeoutSeconds",
                "MenuTimeoutSeconds",
                "BatchRefresh",
                "Concise"
            )
        }
        "Unity.Play" = @{
            Risk = "UnityMutation"
            Implementation = "safe-unity.ps1"
            Required = @()
            Allowed = @("PlayAction", "TimeoutSeconds", "PrintOutput")
        }
        "Token.Summary" = @{
            Risk = "ReadOnly"
            Implementation = "token-report-summary.ps1"
            Required = @()
            Allowed = @(
                "Days",
                "Recent",
                "SinceLastStart",
                "FailedOnly",
                "MaxResults"
            )
        }
        "Token.Start" = @{
            Risk = "WritesReport"
            Implementation = "start-task-token-check.ps1"
            Required = @("Task")
            Allowed = @("Task", "UiPercent", "BudgetTokens", "IncludeUnity")
        }
        "Token.End" = @{
            Risk = "WritesReport"
            Implementation = "end-token-check.ps1"
            Required = @()
            Allowed = @(
                "CurrentPercent",
                "StartPercent",
                "BudgetTokens",
                "CoverageNote",
                "IncludeUnity"
            )
        }
        "Project.Weight" = @{
            Risk = "ReadOnly"
            Implementation = "project-weight-report.ps1"
            Required = @()
            Allowed = @("MaxResults")
        }
        "Benchmark.ReadCost" = @{
            Risk = "WritesReport"
            Implementation = "read-cost-benchmark.ps1"
            Required = @()
            Allowed = @("BaselineRef", "ReportPath")
        }
        "Benchmark.SummaryCache" = @{
            Risk = "WritesReport"
            Implementation = "summary-cache-benchmark.ps1"
            Required = @()
            Allowed = @("ReportPath")
        }
        "Test.Commands" = @{
            Risk = "ReadOnly"
            Implementation = "command-tools-self-test.ps1"
            Required = @()
            Allowed = @()
        }
    }
}
