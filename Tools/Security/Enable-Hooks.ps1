$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'install_hooks.py')
exit $LASTEXITCODE
