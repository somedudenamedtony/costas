Costas for Linux (preview)
==========================

This is a receive-only preview. Costas decodes, shows stations, logs, uploads to QRZ and reports to PSK Reporter, but
it does not transmit on Linux yet: Call, Tune and the transmit test are refused with a message saying so. Transmit
will be turned on once the Linux audio has been checked with a real radio.

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
it over this folder. Your log and settings are kept in ~/.local/share/Costas and are not touched.

Licence: GNU GPL v3 or later (see LICENSE). User manual: https://somedudenamedtony.github.io/costas/
