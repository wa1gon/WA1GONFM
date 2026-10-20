# WA1GONFM
WA1GON File Manager

A tiny file manager rooted at a chosen directory, with a Python API and a small CLI.

Usage

- Install locally: `python -m pip install -e .`
- CLI examples:
  - `wa1gonfm --root /tmp/workspace ls`
  - `wa1gonfm --root /tmp/workspace pwd`
  - `wa1gonfm --root /tmp/workspace cd some/directory`
  - `wa1gonfm --root /tmp/workspace tree`

Python API

```python
from wa1gonfm import FileManager

manager = FileManager("/tmp/workspace")
manager.mkdir("projects")
manager.write_text("projects/hello.txt", "hello world")
print(manager.read_text("projects/hello.txt"))
```
