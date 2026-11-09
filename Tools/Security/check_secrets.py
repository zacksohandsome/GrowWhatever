"""Offline, value-redacting checks. Requires Python 3. No third-party packages."""
import argparse
import collections
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
RULES = {
    "private-key": r"-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----",
    "github-token": r"\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b",
    "provider-token": r"\b(?:sk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{24,}|xox[baprs]-[A-Za-z0-9-]{20,}|AIza[A-Za-z0-9_-]{30,}|AKIA[A-Z0-9]{16})\b",
    "jwt": r"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b",
    "credential-url": r"\b[a-z][a-z0-9+.-]{0,31}://[^\s/:@]{1,256}:[^\s/@]{1,1024}@",
    "assigned-secret": r'''(?i)["']?\b(?:api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|passwd|authorization|cookie|private[_-]?key)\b["']?\s*[:=]\s*["']([^"'\r\n]{8,})["']''',
    "query-secret": r"(?i)[?&](?:api[_-]?key|token|access_token|sig|password|secret)=[A-Za-z0-9%_+./=-]{8,}",
    "email-review": r"\b[A-Za-z0-9._%+-]{1,64}@[A-Za-z0-9.-]{1,253}\.[A-Za-z]{2,63}\b",
    "user-path-review": r"(?i)(?:[A-Z]:[\\/]+(?:Users|Documents and Settings)[\\/]+|/Users/|/home/)[^\s\\/<>\"'\[\]()|]+",
}
PATTERNS = {k: re.compile(v) for k, v in RULES.items()}
SKIP = {".git", "Library", "Temp", "obj", ".vs"}
SENSITIVE = re.compile(r"(?i)(?:^|/)(?:\.env(?:\..+)?|\.npmrc|\.pypirc|credentials(?:\..+)?|id_(?:rsa|ed25519|ecdsa)|[^/]+\.(?:p12|pfx|jks|keystore|key|pem))$")
PLACEHOLDER = re.compile(r"(?i)^(?:<[A-Z][A-Z0-9_ -]+>|\$\{[A-Z_][A-Z0-9_]*\}|\$env:[A-Z_][A-Z0-9_]*|YOUR_[A-Z_]+|REPLACE_ME|PLACEHOLDER|EXAMPLE|REDACTED|\*+)$")


def git(*args, stdin=None):
    result = subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}", *args],
                            cwd=ROOT, input=stdin, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError("Git operation failed; raw diagnostics suppressed")
    return result.stdout


def repository_matches_target():
    result = subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}",
                             "rev-parse", "--show-toplevel"], cwd=ROOT,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    return result.returncode == 0 and Path(os.fsdecode(result.stdout).strip()).resolve() == ROOT


class Scan:
    def __init__(self):
        self.findings = []
        self.gaps = []
        self.manual_reviews = []
        self.counts = collections.Counter()

    def content(self, data, path, scope):
        self.counts[scope] += 1
        for kind, pattern in PATTERNS.items():
            if pattern.search(path):
                self.findings.append(dict(path="<redacted-sensitive-path>", scope=scope, line=0, type="path-" + kind))
        if SENSITIVE.search(path):
            self.findings.append(dict(path=path, scope=scope, line=0, type="credential-file-review"))
            return
        if data.startswith(b"version https://git-lfs.github.com/spec/v1\n"):
            match = re.search(rb"oid sha256:([a-f0-9]{64})", data)
            if not match:
                self.gaps.append(dict(path=path, scope=scope, type="invalid-lfs-pointer"))
                return
            oid = match[1].decode()
            store = Path(os.fsdecode(git("rev-parse", "--git-path", "lfs/objects")).strip())
            if not store.is_absolute():
                store = ROOT / store
            target = store / oid[:2] / oid[2:4] / oid
            if not target.is_file():
                self.gaps.append(dict(path=path, scope=scope, type="missing-local-lfs-object"))
                return
            if target.stat().st_size > 16 * 1024 * 1024:
                self.gaps.append(dict(path=path, scope=scope, type="lfs-payload-over-16MiB"))
                return
            data = target.read_bytes()
            if hashlib.sha256(data).hexdigest() != oid:
                self.gaps.append(dict(path=path, scope=scope, type="lfs-integrity-failure"))
                return
            self.counts["lfs-payloads"] += 1
        if len(data) > 16 * 1024 * 1024:
            self.gaps.append(dict(path=path, scope=scope, type="file-over-16MiB"))
            return
        try:
            text = data.decode("utf-16") if data.startswith((b"\xff\xfe", b"\xfe\xff")) else data.decode("utf-8-sig")
            binary = "\0" in text
        except UnicodeError:
            # Survivor-style fallback: still inspect readable strings in every payload.
            # Replacement preserves byte boundaries instead of joining fragments across bad bytes.
            text = data.decode("utf-8", errors="replace")
            binary = True
        if binary:
            self.manual_reviews.append(dict(path=path, scope=scope, type="binary-content-manual-review"))
            self.counts["binary-text-scans"] += 1
        # Text is scanned regardless of extension, including Shader Graph and future formats.
        for kind, pattern in PATTERNS.items():
            for match in pattern.finditer(text):
                if kind == "assigned-secret" and PLACEHOLDER.fullmatch(match[1]):
                    continue
                self.findings.append(dict(path=path, scope=scope,
                                          line=0 if binary else text.count("\n", 0, match.start()) + 1, type=kind))

    def worktree(self):
        for folder, dirs, files in os.walk(ROOT, followlinks=False):
            for name in list(dirs):
                p = Path(folder) / name
                if p.is_symlink() or p.is_junction():
                    dirs.remove(name)
                    self.gaps.append(dict(path=p.relative_to(ROOT).as_posix(), scope="worktree", type="link-not-followed"))
                elif name in SKIP:
                    dirs.remove(name)
                    self.gaps.append(dict(path=p.relative_to(ROOT).as_posix(), scope="worktree", type="excluded-generated-or-git-directory"))
            for name in files:
                p = Path(folder) / name
                path = p.relative_to(ROOT).as_posix()
                if p.is_symlink():
                    self.gaps.append(dict(path=path, scope="worktree", type="link-not-followed"))
                    continue
                try:
                    if p.stat().st_size > 16 * 1024 * 1024:
                        self.gaps.append(dict(path=path, scope="worktree", type="file-over-16MiB"))
                    else:
                        self.content(p.read_bytes(), path, "worktree")
                except OSError:
                    self.gaps.append(dict(path=path, scope="worktree", type="unreadable-file"))

    def index(self):
        entries = git("ls-files", "--stage", "-z").split(b"\0")
        for entry in filter(None, entries):
            meta, path = entry.split(b"\t", 1)
            mode, oid, stage = meta.split()
            path = path.decode("utf-8", "surrogateescape")
            if stage != b"0" or mode in (b"120000", b"160000"):
                self.gaps.append(dict(path=path, scope="index", type="unmerged-link-or-submodule"))
            else:
                self.content(git("cat-file", "blob", oid.decode()), path, "index")

    def connection_settings(self):
        # Restrict fields before reading; never print remote URLs or header values.
        result = subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}",
                                 "config", "--local", "--get-regexp",
                                 r"^(remote\..*\.(url|pushurl)|http\..*extraheader)$"],
                                cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if result.returncode not in (0, 1):
            raise RuntimeError("Could not check local Git connection settings")
        for number, row in enumerate(result.stdout.splitlines(), 1):
            key, _, value = row.partition(b" ")
            location = f"git-connection-field-{number}.txt"
            self.content(value, location, "git-connection-settings")
            if b"extraheader" in key.lower() and value.strip():
                self.findings.append(dict(path=location, scope="git-connection-settings", line=1, type="authorization-header-review"))

    def tree(self, treeish, scope, seen):
        for entry in filter(None, git("ls-tree", "-r", "-z", treeish).split(b"\0")):
            meta, path = entry.split(b"\t", 1)
            mode, kind, oid = meta.split()
            key = (oid, path)
            if key in seen:
                continue
            seen.add(key)
            path = path.decode("utf-8", "surrogateescape")
            if kind != b"blob" or mode == b"120000":
                self.gaps.append(dict(path=path, scope=scope, type="link-or-submodule"))
            else:
                self.content(git("cat-file", "blob", oid.decode()), path, scope)

    def history(self, revisions):
        seen = set()
        if "--all" in revisions:
            for row in git("for-each-ref", "--format=%(objecttype) %(objectname)").splitlines():
                kind, oid = row.split()
                if kind == b"tree":
                    self.tree(oid.decode(), "ref-tree/" + oid.decode(), seen)
                    self.counts["ref-trees"] += 1
                elif kind != b"commit":
                    self.gaps.append(dict(path="<non-commit-ref>", scope="history/" + oid.decode(), type="non-commit-ref-needs-review"))
        for revision in revisions:
            if revision.startswith("--") or revision.startswith("^"):
                continue
            # The fallback scanner does not claim coverage of tag messages or non-commit refs.
            kind = git("cat-file", "-t", revision).strip()
            if kind != b"commit":
                self.gaps.append(dict(path="<outgoing-ref>", scope="push", type="non-commit-ref-needs-review"))
                return
        commits = git("rev-list", *revisions).decode().splitlines()
        for commit in commits:
            # Commit messages are scanned locally; only the commit ID is a location.
            self.content(git("show", "-s", "--format=%B", commit), f"commit/{commit}.txt", "messages")
            self.tree(commit, f"history/{commit}", seen)
        self.counts["commits"] += len(commits)


def main():
    global ROOT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", help="Explicit Unity project root; defaults to deployed Tools/Security location")
    parser.add_argument("--mode", choices=["worktree", "staged", "history", "all", "push", "message"], default="all")
    parser.add_argument("--message-file")
    args = parser.parse_args()
    if args.root:
        ROOT = Path(args.root).resolve()
    elif Path(__file__).parent.name != "Security" or Path(__file__).parent.parent.name != "Tools":
        raise RuntimeError("Bundled scanner requires explicit --root; implicit root is only for deployed Tools/Security")
    if not ROOT.is_dir():
        raise RuntimeError("Target does not exist")
    repository = repository_matches_target()
    if args.mode in ("all", "staged", "history", "push") and not repository:
        raise RuntimeError("Git root must exactly match target; use worktree mode for non-repositories")
    scan = Scan()
    if args.mode in ("worktree", "all"):
        scan.worktree()
        if repository:
            scan.connection_settings()
        else:
            scan.counts["git-not-applicable"] += 1
    if args.mode in ("staged", "all"):
        scan.index()
    if args.mode in ("history", "all"):
        scan.history(["--all"])
    if args.mode == "message":
        scan.content(Path(args.message_file).read_bytes(), "proposed-commit-message.txt", "message")
    if args.mode == "push":
        for line in sys.stdin:
            if not line.strip():
                continue
            fields = line.split()
            if len(fields) != 4 or not all(re.fullmatch(r"[0-9a-f]{40,64}", fields[i]) for i in (1, 3)):
                raise RuntimeError("Invalid pre-push input")
            _, local, _, remote = fields
            if set(local) == {"0"}:
                continue
            # A new ref scans all reachable history; known remote tips bound updates.
            revisions = [local]
            if set(remote) != {"0"}:
                try:
                    git("cat-file", "-e", remote + "^{commit}")
                    revisions.append("^" + remote)
                except RuntimeError:
                    pass
            scan.history(revisions)
    blocking_gaps = [g for g in scan.gaps if g["type"] != "excluded-generated-or-git-directory"]
    for item in scan.findings + scan.gaps + scan.manual_reviews:
        if any(pattern.search(item["path"]) for pattern in PATTERNS.values()):
            item["path"] = "<redacted-sensitive-path>"
    print(json.dumps(dict(mode=args.mode, counts=scan.counts, findings=scan.findings,
                          gaps=scan.gaps, manual_reviews=scan.manual_reviews,
                          passed=not scan.findings and not blocking_gaps), ensure_ascii=True, indent=2))
    return 1 if scan.findings or blocking_gaps else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        print('{"passed":false,"error":"Scan incomplete; raw exception suppressed"}')
        sys.exit(2)
