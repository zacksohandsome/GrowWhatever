"""Synthetic tests only; no real credentials or network access."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("scanner", Path(__file__).with_name("check_secrets.py"))
scanner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scanner)


def synthetic_assignment():
    # Build synthetic matching content in memory rather than storing token-shaped fixtures.
    return b"pass" + b"word" + b' = "' + b"fake-pass-for-test" + b'"'


class SecurityTests(unittest.TestCase):
    def scan(self, data, path="synthetic.txt"):
        result = scanner.Scan()
        result.content(data, path, "test")
        return result

    def test_detects_token_without_returning_value(self):
        value = "ghp_" + "SYNTHETIC" * 5
        result = self.scan(value.encode())
        self.assertEqual(result.findings[0]["type"], "github-token")
        self.assertNotIn(value, str(result.findings))

    def test_placeholder_and_clean_text(self):
        self.assertFalse(self.scan(b'api_key = "<API_KEY_PLACEHOLDER>"').findings)
        self.assertFalse(self.scan(b"normal project text").findings)

    def test_assigned_password(self):
        self.assertEqual(self.scan(synthetic_assignment()).findings[0]["type"], "assigned-secret")

    def test_private_key(self):
        value = b"-----BEGIN " + b"PRIVATE KEY-----"
        self.assertEqual(self.scan(value).findings[0]["type"], "private-key")

    def test_binary_and_unknown_formats_block(self):
        self.assertTrue(self.scan(b"\x00binary").gaps)
        self.assertTrue(self.scan(b"unknown", "synthetic.unknown").gaps)

    def test_utf16(self):
        self.assertTrue(self.scan(synthetic_assignment().decode().encode("utf-16")).findings)

    def test_sensitive_filename(self):
        self.assertEqual(self.scan(b"", ".env").findings[0]["type"], "credential-file-review")

    def test_sensitive_path_redacted(self):
        path = "ghp_" + "SYNTHETIC" * 5 + ".txt"
        result = self.scan(b"clean", path)
        self.assertEqual(result.findings[0]["path"], "<redacted-sensitive-path>")

    def test_missing_lfs_payload_blocks(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(scanner, "ROOT", Path(folder)):
            pointer = b"version https://git-lfs.github.com/spec/v1\noid sha256:" + b"0" * 64 + b"\nsize 12\n"
            with patch.object(scanner, "git", return_value=b".git/lfs/objects\n"):
                self.assertEqual(self.scan(pointer).gaps[0]["type"], "missing-local-lfs-object")

    def test_history_scans_deleted_versions_and_messages(self):
        first, second = "1" * 40, "2" * 40
        blob = "3" * 40
        def fake_git(*args):
            if args[0] == "for-each-ref":
                return b""
            if args[0] == "rev-list":
                return (second + "\n" + first + "\n").encode()
            if args[0] == "show":
                return b"synthetic message"
            if args[0] == "ls-tree":
                return b"" if args[-1] == second else ("100644 blob " + blob + "\tdeleted.txt\0").encode()
            if args[0] == "cat-file":
                return synthetic_assignment()
            raise AssertionError("Unexpected operation")
        with patch.object(scanner, "git", fake_git):
            result = scanner.Scan()
            result.history(["--all"])
            self.assertEqual(result.counts["messages"], 2)
            self.assertEqual(result.findings[0]["path"], "deleted.txt")

    def test_annotated_tags_are_not_silently_accepted(self):
        with patch.object(scanner, "git", return_value=b"tag\n"):
            result = scanner.Scan()
            result.history(["1" * 40])
            self.assertEqual(result.gaps[0]["type"], "non-commit-ref-needs-review")

    def test_tree_refs_are_scanned(self):
        oid = "4" * 40
        def fake_git(*args):
            if args[0] == "for-each-ref":
                return ("tree " + oid + "\n").encode()
            if args[0] == "rev-list":
                return b""
            if args[0] == "ls-tree":
                return ("100644 blob " + "5" * 40 + "\tsnapshot.txt\0").encode()
            if args[0] == "cat-file":
                return synthetic_assignment()
            raise AssertionError("Unexpected operation")
        with patch.object(scanner, "git", fake_git):
            result = scanner.Scan()
            result.history(["--all"])
            self.assertEqual(result.findings[0]["path"], "snapshot.txt")


if __name__ == "__main__":
    unittest.main()
