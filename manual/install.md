# Install

## Download

1. Open the [Releases page](https://github.com/somedudenamedtony/costas/releases/latest).
2. Under **Assets**, download `Costas-Setup-<version>.exe`.
3. Run it. The installer does not need administrator rights: by default it installs for your user only. It can
   add a desktop shortcut if you tick the box.

Everything Costas needs is in the installer: the WSJT-X `jt9` decoder and Hamlib's `rigctld` for radio control are
bundled. You do not need WSJT-X installed.

!!! warning "Windows SmartScreen"
    The installer is not code-signed yet, so Windows may show *Windows protected your PC*. Click **More info**, then
    **Run anyway**. Each release also has a `.sha256` file if you want to check the download.

Requirements: 64-bit Windows 10 (version 1809) or later, or Windows 11.

## Updates

Costas checks GitHub for a newer build when it starts and then once a day. When one is available, a bar
appears at the top of the main window:

![The update bar](images/update-bar.png)

- **Update** downloads the new installer, checks its SHA-256 checksum, closes Costas, and upgrades it. A small
  *Setup - Costas* window shows the progress; there is nothing to click. Costas opens again when it has finished. If you installed for all users, Windows asks for administrator
  permission first. It is refused while you are transmitting or in a contact: finish or halt first.
- **Release notes** opens the release on GitHub.
- **Skip this version** hides the bar until a newer build comes out.

You can turn the daily check off, or check by hand, in **Settings › Updates**. **☰ › About** also has a **Check for updates** button, which checks even when the daily check is off.

Upgrading keeps your log and settings. An install from before the rename (when the app was called "FT8 Client") is
upgraded too, and its data is moved to the Costas data folder on first start.

## Where your data lives

| What | Where |
| --- | --- |
| Log, settings, decode journal, saved audio | `%LOCALAPPDATA%\Costas\` (for example `C:\Users\you\AppData\Local\Costas\`) |
| QRZ API key and password | Windows Credential Manager, never in a file |
| The program | `%LOCALAPPDATA%\Programs\Costas\` for a per-user install (the default), `C:\Program Files\Costas\` if you chose to install for all users |

Uninstalling removes the program but keeps your data folder, so a reinstall picks up where you left off.

## Linux (preview) {#linux-preview}

Each release also has a Linux build, `Costas-<version>-linux-x64.tar.gz`. It is a **receive-only preview**: Costas
decodes, ranks stations, logs, uploads to QRZ and reports to PSK Reporter, but it does not transmit on Linux yet.
**Call**, **Tune** and the transmit test say *the Linux version is a receive-only preview* and the radio is never
keyed. Transmit will be turned on once the Linux audio has been checked with a real radio.

1. Install what Costas uses from your distribution. On Debian and Ubuntu:

        sudo apt install wsjtx libhamlib-utils libportaudio2 libsecret-tools

    `wsjtx` provides the `jt9` decoder, `libhamlib-utils` provides `rigctld` for radio control, `libportaudio2` is
    the audio library, and `libsecret-tools` lets Costas keep your QRZ key in the desktop keyring (without it, the
    key is asked for again each time Costas starts).
2. For a USB CAT cable, add yourself to the `dialout` group (`sudo usermod -aG dialout $USER`), then log out and in.
3. Download the tarball from the [Releases page](https://github.com/somedudenamedtony/costas/releases/latest),
   unpack it and run `./Costas`. `README.txt` inside says how to add it to your desktop's menu.

In Setup's audio step, pick the radio's USB sound card (often *USB Audio CODEC*). If it is busy because PipeWire or
PulseAudio holds it, pick **pulse** or **pipewire** and choose the radio's sound card in your desktop's sound
settings instead.

Updates: the update bar works the same, but **Update** opens the release page; download the new tarball and unpack
it over the old folder. Your log and settings live in `~/.local/share/Costas/` and are kept.

## Next

Start Costas. The first time, it opens [Set up Costas](setup.md).
