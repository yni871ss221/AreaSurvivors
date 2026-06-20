param([int]$OlderThanDays = 1, [switch]$WhatIf)
& "$PSScriptRoot\Archive-TokenReports.ps1" -OlderThanDays $OlderThanDays -WhatIf:$WhatIf
