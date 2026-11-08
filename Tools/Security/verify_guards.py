"""Exercise installed commit guards without committing, staging or contacting a remote."""
from pathlib import Path
import json
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
GIT = ["git", "-c", f"safe.directory={ROOT.as_posix()}"]


def run(args):
    return subprocess.run(args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def main():
    configured = run(GIT + ["config", "--get", "core.hooksPath"])
    if configured.stdout.strip() != b".githooks":
        raise RuntimeError("Versioned hooks are not enabled")
    result = run(GIT + ["hook", "run", "pre-commit"])
    if result.returncode:
        raise RuntimeError("Index guard did not pass")
    with tempfile.TemporaryDirectory() as folder:
        message = Path(folder) / "synthetic-message.txt"
        message.write_text("Synthetic clean commit message", encoding="utf-8")
        if run(GIT + ["hook", "run", "commit-msg", "--", str(message)]).returncode:
            raise RuntimeError("Clean commit message did not pass")
        value = "ghp_" + "SYNTHETIC" * 5
        message.write_text(value, encoding="utf-8")
        result = run(GIT + ["hook", "run", "commit-msg", "--", str(message)])
        if result.returncode == 0 or value.encode() in result.stdout + result.stderr:
            raise RuntimeError("Sensitive commit guard or redaction failed")
    state_location = run(GIT + ["rev-parse", "--git-path", "security-hooks/state.json"]).stdout.decode().strip()
    state = json.loads((ROOT / state_location).read_text(encoding="utf-8"))
    predecessor = state["predecessors"].get("pre-push")
    if predecessor:
        original = Path(predecessor).read_bytes()
        if not original.decode().rstrip().endswith('git lfs pre-push "$@"'):
            raise RuntimeError("Original LFS hook unavailable")
    elif run(GIT + ["lfs", "version"]).returncode:
        raise RuntimeError("LFS fallback unavailable")
    revision = run(GIT + ["rev-parse", "HEAD"]).stdout.decode().strip()
    update = f"refs/heads/synthetic {revision} refs/heads/synthetic {'0' * 40}\n".encode()
    result = subprocess.run(["python", str(ROOT / "Tools/Security/check_secrets.py"), "--mode", "push"],
                            cwd=ROOT, input=update, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError("Synthetic new-branch push range did not pass")
    print("PASS: installed index guard, clean and blocked commit messages, redaction, preserved LFS hook and new-branch push range. No commit or upload performed.")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("FAIL: guard verification incomplete; raw diagnostics suppressed.")
        raise SystemExit(1)
