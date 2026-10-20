from __future__ import annotations

import argparse
import os

from .manager import FileManager


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="WA1GON File Manager")
    parser.add_argument("--root", default=os.getcwd(), help="Root directory the manager should operate on")
    subparsers = parser.add_subparsers(dest="command", required=True)

    ls_parser = subparsers.add_parser("ls", help="List the current directory")
    ls_parser.add_argument("path", nargs="?", default=".", help="Directory to list")
    ls_parser.add_argument("--all", action="store_true", help="Include hidden files")

    pwd_parser = subparsers.add_parser("pwd", help="Print working directory")
    _ = pwd_parser
    cd_parser = subparsers.add_parser("cd", help="Change working directory")
    cd_parser.add_argument("path")

    mkdir_parser = subparsers.add_parser("mkdir", help="Create directories")
    mkdir_parser.add_argument("path")

    touch_parser = subparsers.add_parser("touch", help="Create or update an empty file")
    touch_parser.add_argument("path")

    rm_parser = subparsers.add_parser("rm", help="Remove a file or directory")
    rm_parser.add_argument("path")

    mv_parser = subparsers.add_parser("mv", help="Move or rename a file or directory")
    mv_parser.add_argument("src")
    mv_parser.add_argument("dst")

    cat_parser = subparsers.add_parser("cat", help="Display a file's content")
    cat_parser.add_argument("path")

    tree_parser = subparsers.add_parser("tree", help="Print a tree view of a directory")
    tree_parser.add_argument("path", nargs="?", default=".", help="Directory to display")
    tree_parser.add_argument("--all", action="store_true", help="Include hidden files")

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)

    manager = FileManager(args.root)

    command = args.command
    if command == "ls":
        items = manager.list(args.path, include_hidden=args.all)
        for item in items:
            suffix = "/" if item.is_dir else ""
            print(f"{item.name}{suffix}")
        return 0

    if command == "pwd":
        print(manager.pwd())
        return 0

    if command == "cd":
        print(manager.cd(args.path))
        return 0

    if command == "mkdir":
        manager.mkdir(args.path)
        return 0

    if command == "touch":
        manager.touch(args.path)
        return 0

    if command == "rm":
        manager.remove(args.path)
        return 0

    if command == "mv":
        manager.move(args.src, args.dst)
        return 0

    if command == "cat":
        print(manager.read_text(args.path), end="")
        return 0

    if command == "tree":
        for line in manager.tree(args.path, include_hidden=args.all):
            print(line)
        return 0

    parser.error(f"Unsupported command: {command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
