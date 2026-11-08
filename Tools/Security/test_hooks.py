"""Synthetic tests for preservation, fail-closed dispatch and pre-push stdin."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


installer = load("install_hooks")
runner = load("run_hook")


class HookTests(unittest.TestCase):
    def test_recognizes_only_safe_lfs_shell(self):
        safe = '#!/bin/sh\ncommand -v git-lfs >/dev/null 2>&1 || { printf >&2 "%s\\n" "Synthetic missing tool message"; exit 2; }\ngit lfs pre-push "$@"\n'
        self.assertTrue(installer.is_lfs(safe, "pre-push"))
        self.assertFalse(installer.is_lfs(safe.replace("Synthetic", '"; echo injected; echo "'), "pre-push"))
        self.assertFalse(installer.is_lfs(safe.replace("Synthetic", "$(echo injected)"), "pre-push"))

    def test_unknown_hook_leaves_activation_untouched(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / ".git/hooks").mkdir(parents=True)
            (root / ".git/hooks/pre-commit").write_text("#!/bin/sh\necho synthetic custom hook\n")
            (root / ".githooks").mkdir()
            for event in installer.EVENTS:
                (root / ".githooks" / event).write_text("#!/bin/sh\nexit 0\n")
            calls = []
            def fake_git(*args, **kwargs):
                calls.append(args)
                if args == ("rev-parse", "--show-toplevel"):
                    return str(root)
                if args == ("rev-parse", "--git-common-dir"):
                    return ".git"
                if args == ("rev-parse", "--git-path", "security-hooks/state.json"):
                    return ".git/security-hooks/state.json"
                return ""
            with patch.object(installer, "ROOT", root), patch.object(installer, "git", fake_git):
                with self.assertRaises(RuntimeError):
                    installer.main()
            self.assertFalse((root / ".git/security-hooks/state.json").exists())
            self.assertFalse(any(args[:2] == ("config", "--local") for args in calls))
            self.assertIn("synthetic custom", (root / ".git/hooks/pre-commit").read_text())

    def exercise_dispatch(self, passed, predecessor_code):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "Tools/Security").mkdir(parents=True)
            (root / "Tools/Security/check_secrets.py").write_text("# synthetic scanner\n")
            predecessor = root / "synthetic-predecessor.sh"
            predecessor.write_text("# synthetic predecessor\n")
            state = root / "state.json"
            state.write_text(json.dumps({"version": 1, "predecessors": {"pre-push": str(predecessor)}}))
            updates = b"refs/heads/synthetic " + b"1" * 40 + b" refs/heads/synthetic " + b"0" * 40 + b"\n"
            calls = []
            def fake_run(command, **kwargs):
                calls.append((command, kwargs))
                if len(calls) == 1:
                    return subprocess.CompletedProcess(command, 0 if passed else 1, json.dumps({"passed": passed}).encode(), b"")
                return subprocess.CompletedProcess(command, predecessor_code, b"", b"synthetic diagnostics")
            with patch.object(runner, "ROOT", root), patch.object(runner, "state_path", return_value=state), patch.object(runner.subprocess, "run", fake_run), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                result = runner.dispatch("pre-push", ["synthetic-origin", "synthetic-address"], updates)
            self.assertEqual(calls[0][1]["input"], updates)
            if passed:
                self.assertEqual(calls[1][1]["input"], updates)
                self.assertEqual(calls[1][0][-2:], ["synthetic-origin", "synthetic-address"])
                self.assertEqual(result, predecessor_code)
            else:
                self.assertEqual(len(calls), 1)
                self.assertEqual(result, 1)

    def test_push_stdin_and_arguments_are_preserved(self):
        self.exercise_dispatch(True, 0)

    def test_predecessor_failure_is_preserved(self):
        self.exercise_dispatch(True, 7)

    def test_failed_scan_stops_predecessor(self):
        self.exercise_dispatch(False, 0)


if __name__ == "__main__":
    unittest.main()
