# Project safety

- Before first Git work or a commit, read `Tools/Security/README.md` and run the documented checks. User and global safety rules continue to apply.
- Scan the actual index, paths and proposed message before committing. Scan the actual outgoing history, including deleted versions and available local LFS content, before pushing.
- Report findings only by safe location, type and count. Redact sensitive paths. Stop the affected commit, push or upload if findings or coverage gaps remain unresolved.
- Keep existing hooks and LFS behavior. Never bypass hooks or add exclusions to evade findings. After cloning, review and run `python Tools/Security/install_hooks.py`; verify `core.hooksPath` is `.githooks`.
- Keep unrelated Unity changes out of security commits. Use `chore(security): ...` for security tooling and documentation maintenance; follow any more specific repository convention if added later.
