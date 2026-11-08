"""Enable versioned hooks locally without replacing existing hook files."""
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
EVENTS = ("pre-commit", "commit-msg", "pre-push", "post-checkout", "post-commit", "post-merge")
LEGACY_PREFIX = '''#!/bin/sh
# Repository security guard. Fail closed if Python or the scanner is unavailable.
root=$(git rev-parse --show-toplevel) || exit 1
scanner="$root/Tools/Security/check_secrets.py"
[ -f "$scanner" ] || { echo 'Security scanner missing; action blocked.' >&2; exit 1; }
'''
LEGACY = {
    "pre-commit": LEGACY_PREFIX + 'python "$scanner" --mode staged || exit 1\n',
    "commit-msg": LEGACY_PREFIX + 'python "$scanner" --mode message --message-file "$1" || exit 1\n',
    "pre-push": LEGACY_PREFIX + '''updates=$(cat) || exit 1
printf '%s\\n' "$updates" | python "$scanner" --mode push || exit 1
printf '%s\\n' "$updates" | sh "$root/.git/hooks/pre-push.before-security" "$@"
''',
}


def git(*args, optional=False):
    result = subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}", *args], cwd=ROOT,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode and not (optional and result.returncode == 1):
        raise RuntimeError("Git operation failed")
    return result.stdout.decode().strip()


def local_path(path):
    path = Path(path)
    return path if path.is_absolute() else ROOT / path


def is_lfs(text, event):
    # Accept only the known three-line Git LFS installer hook, not arbitrary shell code.
    lines = text.splitlines()
    message = ("\\nThis repository is configured for Git LFS but 'git-lfs' was not found on your path. "
               "If you no longer wish to use Git LFS, remove this hook by deleting "
               f"'.git/hooks/{event}'.\\n")
    echo_line = 'command -v git-lfs >/dev/null 2>&1 || { echo >&2 "' + message + '"; exit 2; }'
    check = r'''command -v git-lfs >/dev/null 2>&1 \|\| \{ printf >&2 "%s\\n" "[^"$`]*"; exit 2; \}'''
    return (len(lines) == 3 and lines[0] == "#!/bin/sh"
            and (lines[1] == echo_line or re.fullmatch(check, lines[1]) is not None)
            and not any(char in lines[1] for char in ("`", "$"))
            and lines[2] == f'git lfs {event} "$@"')


def main():
    if sys.version_info < (3, 12):
        raise RuntimeError("Python 3.12 or later required")
    if Path(git("rev-parse", "--show-toplevel")).resolve() != ROOT:
        raise RuntimeError("Target must be repository root")
    git("lfs", "version")
    for event in EVENTS:
        hook = ROOT / ".githooks" / event
        if not hook.is_file() or hook.is_symlink():
            raise RuntimeError("Versioned hook missing or linked")
    current = git("config", "--get", "core.hooksPath", optional=True)
    state_path = local_path(git("rev-parse", "--git-path", "security-hooks/state.json"))
    if current == ".githooks":
        state = json.loads(state_path.read_text(encoding="utf-8"))
        if state.get("version") != 1 or any(not Path(p).is_file() for p in state["predecessors"].values()):
            raise RuntimeError("Existing hook state invalid")
        print("Versioned hooks already enabled; retained hooks unchanged.")
        return
    if current:
        raise RuntimeError("Custom hooksPath requires review before integration")
    common = local_path(git("rev-parse", "--git-common-dir"))
    original_hooks = common / "hooks"
    predecessors = {}
    if original_hooks.exists():
        for hook in original_hooks.iterdir():
            if hook.name.endswith(".sample") or hook.name == "pre-push.before-security":
                continue
            if not hook.is_file() or hook.is_symlink() or hook.name not in EVENTS:
                raise RuntimeError("Unrecognized existing hook requires review")
            text = hook.read_text(encoding="utf-8").replace("\r\n", "\n")
            if is_lfs(text, hook.name):
                predecessors[hook.name] = str(hook.resolve())
            elif text == LEGACY.get(hook.name):
                if hook.name == "pre-push":
                    backup = original_hooks / "pre-push.before-security"
                    if not is_lfs(backup.read_text(encoding="utf-8"), "pre-push"):
                        raise RuntimeError("Original LFS backup requires review")
                    predecessors[hook.name] = str(backup.resolve())
            else:
                raise RuntimeError("Unrecognized existing hook requires review")
    if state_path.exists():
        raise RuntimeError("Unexpected existing state requires review")
    state_path.parent.mkdir(parents=True, exist_ok=True)
    state_path.write_text(json.dumps({"version": 1, "predecessors": predecessors}, indent=2), encoding="utf-8")
    # Activate only after every predecessor is accounted for; originals remain untouched.
    git("config", "--local", "core.hooksPath", ".githooks")
    if git("config", "--get", "core.hooksPath") != ".githooks":
        raise RuntimeError("Activation verification failed")
    print(f"Enabled .githooks; retained {len(predecessors)} existing LFS hooks without modifying them.")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("Hook activation stopped; existing hooks retained. Review locally; raw diagnostics suppressed.", file=sys.stderr)
        raise SystemExit(1)
