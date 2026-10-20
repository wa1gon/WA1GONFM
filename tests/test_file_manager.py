from pathlib import Path

from wa1gonfm import FileManager


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


def test_cli_ls_smoke(tmp_path: Path, monkeypatch, capsys):
    monkeypatch.chdir(tmp_path)

    from wa1gonfm.cli import main

    assert main(["ls"]) == 0
    assert "" in capsys.readouterr().out

    (tmp_path / "nested").mkdir()
    assert main(["ls", "nested"]) == 0
