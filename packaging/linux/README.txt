Costas for Linux (preview)
==========================

This is a preview. Costas receives and transmits FT8, FT4, PSK31, PSK63 and RTTY on Linux, but it has not yet been
used with a real radio on Linux: on your first contacts, run the transmit test in Setup and watch the radio's ALC and
power.

Install what Costas uses from your distribution (Debian and Ubuntu names):

    sudo apt install wsjtx libhamlib-utils libportaudio2 libsecret-tools

wsjtx provides the jt9 decoder, libhamlib-utils provides rigctld for radio control, libportaudio2 is the audio
library, and libsecret-tools lets Costas keep your QRZ key in the desktop keyring. To use a serial port (your
radio's CAT cable), your user needs to be in the dialout group:

    sudo usermod -aG dialout $USER     (then log out and back in)

Start Costas by running ./Costas in this folder. To add it to your desktop's menu, copy costas.desktop to
~/.local/share/applications/ and costas.png to ~/.local/share/icons/, and change Exec= in costas.desktop to the full
path of Costas.

In Setup's audio step, pick your radio's USB sound card (often "USB Audio CODEC"). If the device is busy because
PipeWire or PulseAudio holds it, pick "pulse" or "pipewire" instead and choose the radio's sound card in your
desktop's sound settings.

Updates: Costas tells you when a newer build is out and opens the release page; download the new tarball and unpack
it. It unpacks into a new folder named for its version: run ./Costas from there, then delete this folder. If you added
Costas to your desktop's menu, change Exec= in ~/.local/share/applications/costas.desktop to the new folder. Your log
and settings are kept in ~/.local/share/Costas and are not touched.

Licence: GNU GPL v3 or later (see LICENSE). User manual: https://somedudenamedtony.github.io/costas/
