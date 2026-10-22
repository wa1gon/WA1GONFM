# filepilot v0.7.15

- Identify text ADIF `.adi` amateur-radio logs by bounded content inspection rather than file extension alone.
- Prefer validated ADIF over libmagic AAC misclassification.
- Show "Amateur Radio ADIF Log" in the Type column.
- Preserve generic MIME detection for non-ADIF `.adi` files.
- Add ADIF parser tests for valid, malformed, and binary content.
- Bump application version to 0.7.15.
