param(
    [ValidateSet('all','worktree','staged','history','push','message')]
    [string]$Mode = 'all',
    [string]$MessageFile
)
$ErrorActionPreference = 'Stop'
$scanner = Join-Path $PSScriptRoot 'check_secrets.py'
if ($Mode -eq 'message') {
    python $scanner --mode message --message-file $MessageFile
} elseif ($Mode -eq 'push') {
    $input | python $scanner --mode push
} else {
    python $scanner --mode $Mode
}
exit $LASTEXITCODE
