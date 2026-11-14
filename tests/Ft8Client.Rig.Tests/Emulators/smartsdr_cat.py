# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Minimal SmartSDR CAT COM port emulator on a pseudo-terminal, for tests. SmartSDR CAT's COM ports answer as a Kenwood
# TS-2000 (ID019). Prints the terminal path, then answers the Kenwood commands Hamlib's TS-2000 backend (model 2014)
# uses and logs every command to the file named in argv[1].
import os, sys, tty, select
master, slave = os.openpty()
tty.setraw(slave)
print(os.ttyname(slave), flush=True)
log = open(sys.argv[1], 'w', buffering=1)
st = {'ID': '019', 'FA': '00014074000', 'FB': '00007074000', 'MD': '2', 'AI': '0', 'FR': '0', 'FT': '0', 'PS': '1', 'TX': '0'}
buf = b''
while True:
    r, _, _ = select.select([master], [], [], 0.5)
    if not r: continue
    buf += os.read(master, 1024)
    while b';' in buf:
        cmd, buf = buf.split(b';', 1)
        c = cmd.decode(errors='replace')
        log.write(c + ';\n')
        reply = ''
        if c == 'IF':
            # IF: frequency, step, RIT/XIT, memory channel, TX/RX (index 28), mode (29), VFO (30), scan, split (32), tone.
            reply = f"IF{st['FA']}     +000000000{st['TX']}{st['MD']}{st['FR']}0{'1' if st['FR'] != st['FT'] else '0'}0000;"
        elif c in ('TX', 'TX0', 'TX1'):
            st['TX'] = '1'
        elif c == 'RX':
            st['TX'] = '0'
        elif c in st:
            reply = f"{c}{st[c]};"
        elif len(c) > 2 and c[:2] in st and c[:2] not in ('ID', 'TX'):
            st[c[:2]] = c[2:]
        else:
            reply = '?;'
        if reply: os.write(master, reply.encode())
