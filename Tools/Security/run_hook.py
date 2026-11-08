"""Dispatch versioned security guards and retained local Git LFS hooks."""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
EVENTS = {"pre-commit", "commit-msg", "pre-push", "post-checkout", "post-commit", "post-merge"}
LFS_EVENTS = {"pre-push", "post-checkout", "post-commit", "post-merge"}


def git(*args):
    return subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}", *args], cwd=ROOT,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def state_path():
    result = git("rev-parse", "--git-path", "security-hooks/state.json")
    if result.returncode:
        raise RuntimeError("Cannot locate hook state")
    path = Path(result.stdout.decode().strip())
    return path if path.is_absolute() else ROOT / path


def dispatch(event, args, updates=None):
    if event not in EVENTS:
        return 1
    state = json.loads(state_path().read_text(encoding="utf-8"))
    if state.get("version") != 1:
        return 1
    scanner = ROOT / "Tools/Security/check_secrets.py"
    if event in {"pre-commit", "commit-msg", "pre-push"}:
        if not scanner.is_file():
            return 1
        command = [sys.executable, str(scanner), "--mode"]
        command += {"pre-commit": ["staged"], "commit-msg": ["message"], "pre-push": ["push"]}[event]
        if event == "commit-msg":
            if len(args) != 1:
                return 1
            command += ["--message-file", args[0]]
        result = subprocess.run(command, cwd=ROOT, input=updates,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        # Only the scanner's structured, redacted output is forwarded.
        report = json.loads(result.stdout)
        if not isinstance(report.get("passed"), bool):
            return 1
        print(json.dumps(report, ensure_ascii=True))
        if result.returncode or not report["passed"]:
            return 1
    predecessor = state.get("predecessors", {}).get(event)
    if predecessor:
        if not Path(predecessor).is_file():
            return 1
        command = ["sh", predecessor, *args]
    elif event in LFS_EVENTS:
        command = ["git", "-c", f"safe.directory={ROOT.as_posix()}", "lfs", event, *args]
    else:
        return 0
    result = subprocess.run(command, cwd=ROOT, input=updates,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        print("Retained hook failed; raw diagnostics suppressed.", file=sys.stderr)
    return result.returncode


if __name__ == "__main__":
    try:
        event = sys.argv[1]
        updates = sys.stdin.buffer.read() if event == "pre-push" else None
        raise SystemExit(dispatch(event, sys.argv[2:], updates))
    except Exception:
        print("Security hook incomplete; operation blocked. Raw diagnostics suppressed.", file=sys.stderr)
        raise SystemExit(1)
