$solutionPath = Join-Path $PSScriptRoot 'ForgePlm.slnx'

& dotnet test $solutionPath --collect 'XPlat Code Coverage'
exit $LASTEXITCODE
