from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import shutil


@dataclass(frozen=True)
class FileEntry:
    name: str
    path: str
    is_dir: bool
    size: int = 0


class FileManager:
    """A small filesystem manager rooted at a chosen directory."""

    def __init__(self, root: str | Path | None = None):
        base = Path(root) if root is not None else Path.cwd()
        self.root = base.expanduser().resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self.cwd = self.root

    def _resolve(self, target: str | Path | None, *, allow_missing: bool = False) -> Path:
        if target is None:
            candidate = self.cwd
        else:
            resolved = Path(target)
            if not resolved.is_absolute():
                resolved = self.cwd / resolved
            candidate = resolved.expanduser().resolve(strict=False)

        try:
            candidate.relative_to(self.root)
        except ValueError as exc:
            raise ValueError(f"Path escapes manager root: {candidate}") from exc

        if not allow_missing and not candidate.exists():
            raise FileNotFoundError(candidate)

        return candidate

    def pwd(self) -> str:
        try:
            relative = self.cwd.relative_to(self.root)
            return str(relative) if str(relative) != "." else "/"
        except ValueError:
            return str(self.cwd)

    def cd(self, path: str | Path) -> str:
        target = self._resolve(path)
        if not target.is_dir():
            raise NotADirectoryError(target)
        self.cwd = target
        return self.pwd()

    def list(self, path: str | Path | None = None, *, include_hidden: bool = False) -> list[FileEntry]:
        target = self._resolve(path if path is not None else self.cwd)
        if not target.exists():
            raise FileNotFoundError(target)

        entries: list[FileEntry] = []
        for child in sorted(target.iterdir(), key=lambda p: (not p.is_dir(), p.name.lower())):
            if not include_hidden and child.name.startswith("."):
                continue
            if child.is_dir():
                entries.append(FileEntry(child.name, str(child.relative_to(self.root)), True, 0))
            else:
                entries.append(FileEntry(child.name, str(child.relative_to(self.root)), False, child.stat().st_size))
        return entries

    def mkdir(self, path: str | Path, *, parents: bool = True) -> Path:
        target = self._resolve(path, allow_missing=True)
        if target.exists() and not target.is_dir():
            raise FileExistsError(target)
        target.mkdir(parents=parents, exist_ok=True)
        return target

    def touch(self, path: str | Path) -> Path:
        target = self._resolve(path, allow_missing=True)
        if target.exists() and target.is_dir():
            raise IsADirectoryError(target)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.touch(exist_ok=True)
        return target

    def write_text(self, path: str | Path, content: str, *, encoding: str = "utf-8") -> Path:
        target = self._resolve(path, allow_missing=True)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding=encoding)
        return target

    def read_text(self, path: str | Path, *, encoding: str = "utf-8") -> str:
        target = self._resolve(path)
        return target.read_text(encoding=encoding)

    def remove(self, path: str | Path) -> None:
        target = self._resolve(path)
        if target.is_dir() and not target.is_symlink():
            shutil.rmtree(target)
        else:
            target.unlink()

    def rename(self, old_name: str | Path, new_name: str | Path) -> Path:
        src = self._resolve(old_name)
        dst = self._resolve(new_name, allow_missing=True)
        if dst.exists():
            raise FileExistsError(dst)
        dst.parent.mkdir(parents=True, exist_ok=True)
        src.rename(dst)
        return dst

    def move(self, old_name: str | Path, new_name: str | Path) -> Path:
        return self.rename(old_name, new_name)

    def tree(self, path: str | Path | None = None, *, include_hidden: bool = False, prefix: str = "") -> list[str]:
        target = self._resolve(path if path is not None else self.cwd)
        if not target.exists():
            raise FileNotFoundError(target)

        lines: list[str] = []
        for child in sorted(target.iterdir(), key=lambda p: (not p.is_dir(), p.name.lower())):
            if not include_hidden and child.name.startswith("."):
                continue
            marker = "/" if child.is_dir() else ""
            lines.append(f"{prefix}{child.name}{marker}")
            if child.is_dir():
                lines.extend(self.tree(child, include_hidden=include_hidden, prefix=f"{prefix}  "))
        return lines

    def exists(self, path: str | Path) -> bool:
        try:
            self._resolve(path)
            return True
        except (FileNotFoundError, ValueError):
            return False
