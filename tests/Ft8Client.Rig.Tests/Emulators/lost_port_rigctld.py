#!/usr/bin/env python3
# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors
#
# This program is free software: you can redistribute it and/or modify it under the terms of the
# GNU General Public License as published by the Free Software Foundation, either version 3 of the
# License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
# see the GNU General Public License in LICENSE for details.

# A stand-in rigctld whose first run has lost the radio's port: like Hamlib with comm_state 0, it answers PTT with
# RPRT -1. Later runs work. Each run appends "start" and every command to the log file named by COSTAS_FAKE_LOG.
import os, socket, sys

args = sys.argv[1:]
port = int(args[args.index("-t") + 1])
log = os.environ["COSTAS_FAKE_LOG"]
with open(log, "a") as f:
    first = os.path.getsize(log) == 0
    f.write("start\n")

srv = socket.socket()
srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
srv.bind(("127.0.0.1", port))
srv.listen(4)
import threading

def serve(conn):
    buf = conn.makefile("rw", newline="\n")
    for line in buf:
        cmd = line.strip().lstrip("+")
        with open(log, "a") as f:
            f.write(cmd + "\n")
        if cmd == "f":
            buf.write("get_freq:\nFrequency: 14074000\nRPRT 0\n")
        elif cmd.startswith("T ") and first:
            buf.write("set_ptt: %s\nRPRT -1\n" % cmd[2:])
        else:
            buf.write("%s\nRPRT 0\n" % cmd)
        buf.flush()

while True:
    c, _ = srv.accept()
    threading.Thread(target=serve, args=(c,), daemon=True).start()
