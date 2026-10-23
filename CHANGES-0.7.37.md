# filepilot 0.7.37

- Enable **Always use selected application** for manually added executables in Open With.
- Store per-MIME filepilot defaults in `~/.config/filepilot/open-with-apps.json`, separate from Linux MIME defaults.
- Opening a file honors the configured filepilot default; desktop-registered apps use Linux's system MIME default.
- Replacing a filepilot-only preference with a system-registered default clears the filepilot override.
