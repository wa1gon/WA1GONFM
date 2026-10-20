from pathlib import Path

import pytest

from wa1gonfm import FileManager
from wa1gonfm.cli import main


def test_file_manager_lifecycle(tmp_path: Path):
    manager = FileManager(tmp_path)

    manager.mkdir("projects")
    manager.touch("projects/example.txt")
    manager.write_text("projects/example.txt", "hello world")

    entries = {item.name: item for item in manager.list("projects")}
    assert "example.txt" in entries
    assert entries["example.txt"].is_dir is False
    assert manager.read_text("projects/example.txt") == "hello world"

    manager.rename("projects/example.txt", "projects/renamed.txt")
    assert not (tmp_path / "projects" / "example.txt").exists()
    assert (tmp_path / "projects" / "renamed.txt").read_text() == "hello world"

    manager.remove("projects")
    assert not (tmp_path / "projects").exists()


def test_path_escape_is_rejected(tmp_path: Path):
    manager = FileManager(tmp_path)

    with pytest.raises(ValueError):
        manager.list("../../outside")


def test_cli_ls_smoke(tmp_path: Path, monkeypatch, capsys):
    monkeypatch.chdir(tmp_path)

    assert main(["ls"]) == 0
    assert capsys.readouterr().out == ""

    (tmp_path / "nested").mkdir()
    assert main(["--root", str(tmp_path), "ls", "nested"]) == 0
    assert capsys.readouterr().out == ""

    assert main(["--root", str(tmp_path), "ls", "."]) == 0
    assert "nested" in capsys.readouterr().out

    assert main(["--root", str(tmp_path), "mkdir", "newdir"]) == 0
    assert (tmp_path / "newdir").is_dir()

    assert main(["--root", str(tmp_path), "touch", "newdir/file.txt"]) == 0
    assert (tmp_path / "newdir" / "file.txt").exists()

    assert main(["--root", str(tmp_path), "cat", "newdir/file.txt"]) == 0
    assert capsys.readouterr().out == ""

    assert main(["--root", str(tmp_path), "pwd"]) == 0
    assert "/" in capsys.readouterr().out

    assert main(["--root", str(tmp_path), "cd", "newdir"]) == 0
    assert (tmp_path / ".wa1gonfm_state").exists()
