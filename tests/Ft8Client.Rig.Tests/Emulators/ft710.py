# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Minimal Yaesu FT-710 CAT emulator on a pseudo-terminal, for tests. Prints the terminal path, then answers the
# newcat commands Hamlib's FT-710 backend (model 1049) uses and logs every command to the file named in argv[1].
import os, sys, tty, select
master, slave = os.openpty()
tty.setraw(slave)
print(os.ttyname(slave), flush=True)
log = open(sys.argv[1], 'w', buffering=1)
st = {'ID': '0800', 'FA': '014074000', 'FB': '007074000', 'MD0': '2', 'TX': '0', 'AI': '0', 'FT': '0', 'ST': '0',
      'PS': '1', 'VS': '0', 'SH0': '00', 'NA0': '0', 'PC': '050', 'RA0': '0', 'PA0': '0', 'KS': '20', 'BS': '', 'SM0': '0050',
      'RI0': '0', 'FR': '0', 'EX': '', 'RM4': '032000', 'RM6': '032000'}
buf = b''
while True:
    r, _, _ = select.select([master], [], [], 0.5)
    if not r: continue
    buf += os.read(master, 1024)
    while b';' in buf:
        cmd, buf = buf.split(b';', 1)
        c = cmd.decode(errors='replace')
        log.write(c + ';\n')
        key = next((k for k in sorted(st, key=len, reverse=True) if c.startswith(k)), None)
        if c == 'IF':
            reply = f"IF001{st['FA']}+000000{st['MD0']}00000;"
        elif c == 'OI':
            reply = f"OI001{st['FB']}+000000{st['MD0']}00000;"
        elif key is None:
            reply = '?;'
        elif c == key:  # query
            reply = f"{key}{st[key]};"
        else:  # set
            st[key] = c[len(key):]
            reply = ''
        if c == 'TX1' or c == 'TX0':
            st['TX'] = c[2]
        if reply: os.write(master, reply.encode())
