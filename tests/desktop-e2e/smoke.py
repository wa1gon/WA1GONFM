#!/usr/bin/env python3
"""Optional desktop smoke test via AT-SPI. Requires python3-dogtail and an X11 session.
Run WA1GONFM before this script. This is a real desktop check, not a mock.
"""
from dogtail.tree import root
app = root.application('WA1GONFM')
assert app is not None, 'WA1GONFM window not found'
print('PASS: WA1GONFM is visible to AT-SPI')
