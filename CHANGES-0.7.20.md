# filepilot v0.7.20

- Fix the Ext column showing the complete filename for Unix dotfiles such as `.bash_history`.
- Make extension sorting and File Type Integrity use the same corrected extension rules.
- Preserve ordinary compound dotfiles such as `.config.json`, where `.json` is a real extension.
- Add filename-extension unit tests.
